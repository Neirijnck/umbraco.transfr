using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.History;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Transfer;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Persistence;
using Neirijnck.Transfr.Planning;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// Carries out a dry run, to a remote or from it to here: plans again and refuses when anything differs from what the dry run showed,
/// writes row by row (media before the documents that use them, parents first) and stops at the first failure, puts
/// copied nodes in their place among their siblings, and records what it wrote in the copy history. It writes only
/// through the allowlisted upload, create, update and sort requests: nothing is deleted, unpublished or moved.
/// </summary>
public sealed partial class TransferService
{
    // One transfer per remote environment at a time: two at once would each plan against a remote the other changes.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> running = new(StringComparer.Ordinal);

    private readonly CopyPlanService plans;
    private readonly RemoteClient client;
    private readonly RemoteTreeReader remoteTree;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly RemoteMediaReader remoteMedia;
    private readonly MediaFileManager mediaFiles;
    private readonly LocalTreeReader localTree;
    private readonly LocalDocumentReader localDocuments;
    private readonly LocalMediaReader localMedia;
    private readonly LocalTransferServices localTargets;
    private readonly CopyHistoryStore history;
    private readonly ILanguageService languageService;
    private readonly ILogger<TransferService> logger;

    public TransferService(
        CopyPlanService plans,
        RemoteClient client,
        RemoteTreeReader remoteTree,
        RemoteDocumentReader remoteDocuments,
        RemoteMediaReader remoteMedia,
        MediaFileManager mediaFiles,
        LocalTreeReader localTree,
        LocalDocumentReader localDocuments,
        LocalMediaReader localMedia,
        LocalTransferServices localTargets,
        CopyHistoryStore history,
        ILanguageService languageService,
        ILogger<TransferService> logger)
    {
        this.plans = plans;
        this.client = client;
        this.remoteTree = remoteTree;
        this.remoteDocuments = remoteDocuments;
        this.remoteMedia = remoteMedia;
        this.mediaFiles = mediaFiles;
        this.localTree = localTree;
        this.localDocuments = localDocuments;
        this.localMedia = localMedia;
        this.localTargets = localTargets;
        this.history = history;
        this.languageService = languageService;
        this.logger = logger;
    }

    /// <param name="remote">The remote copied to or from.</param>
    /// <param name="direction">ToRemote writes to the remote, FromRemote here.</param>
    /// <param name="user">The backoffice user: a copy to here needs their permission on every item it writes.</param>
    /// <param name="userKey">The backoffice user, for the copy history and as who saves here.</param>
    /// <param name="selection">The selection the dry run was made for.</param>
    /// <param name="dryRun">The rows as the dry run showed them.</param>
    /// <param name="overwrite">Conflicts chosen to overwrite.</param>
    /// <param name="cancellationToken">Stops between rows; a write already sent is not undone.</param>
    /// <exception cref="PlanSelectionException">The selection can't be planned, or overwrite names a node that isn't a conflict.</exception>
    public async Task<TransferResult> TransferAsync(
        RemoteDefinition remote,
        CopyDirection direction,
        ClaimsPrincipal user,
        Guid userKey,
        IReadOnlyList<PlanSelection> selection,
        IReadOnlyList<DryRunRow> dryRun,
        IReadOnlySet<Guid> overwrite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remote);
        var gate = this.running.GetOrAdd(RemoteIdentity.Of(remote.BaseUrl), _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return new TransferResult(TransferStatus.Busy, [], [], 0, "Another transfer with this remote is running.", 0);
        }

        try
        {
            return await this.RunAsync(remote, direction, user, userKey, selection, dryRun, overwrite, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TransferResult> RunAsync(
        RemoteDefinition remote,
        CopyDirection direction,
        ClaimsPrincipal user,
        Guid userKey,
        IReadOnlyList<PlanSelection> selection,
        IReadOnlyList<DryRunRow> dryRun,
        IReadOnlySet<Guid> overwrite,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var (target, files, source) = direction == CopyDirection.FromRemote
            ? ((ITransferTarget)this.localTargets.For(user, userKey),
                (IMediaFileSource)new RemoteMediaFileSource(remote, this.client),
                (IPlanSource)new RemotePlanSource(remote, this.remoteTree, this.remoteDocuments, this.remoteMedia))
            : (new RemoteTransferTarget(remote, this.client, this.remoteTree, this.remoteDocuments, this.remoteMedia),
                new LocalMediaFileSource(this.mediaFiles),
                new LocalPlanSource(this.localTree, this.localDocuments, this.localMedia));
        var plan = await this.plans.PlanAsync(remote, direction, selection, cancellationToken);

        var drift = PlanCheck.Compare(plan.Rows, dryRun);
        if (drift.Count > 0)
        {
            LogRefused(this.logger, direction, remote.Alias, drift.Count);
            return new TransferResult(TransferStatus.Refused, [], drift, 0, null, plan.RemoteRequests);
        }

        var conflicts = plan.Rows.Where(row => row.Outcome == PlanOutcome.Conflict).Select(row => row.Key).ToHashSet();
        var notConflicts = overwrite.Where(key => !conflicts.Contains(key)).ToList();
        if (notConflicts.Count > 0)
        {
            throw new PlanSelectionException("Only conflicts can be chosen to overwrite.", notConflicts);
        }

        var rows = new List<TransferRow>();
        var failed = false;
        foreach (var row in plan.Rows)
        {
            var writes = row.Outcome is PlanOutcome.Create or PlanOutcome.Update
                || (row.Outcome == PlanOutcome.Conflict && overwrite.Contains(row.Key));
            if (!writes)
            {
                var result = row.Outcome switch
                {
                    PlanOutcome.Unchanged => TransferRowResult.Unchanged,
                    PlanOutcome.Blocked => TransferRowResult.Blocked,
                    _ => TransferRowResult.Skipped,
                };
                rows.Add(new TransferRow(row, result, null, null));
            }
            else if (failed || cancellationToken.IsCancellationRequested)
            {
                rows.Add(new TransferRow(row, TransferRowResult.NotRun, null, null));
            }
            else
            {
                var error = row.Kind == PlanItemKind.Media
                    ? await this.WriteMediaAsync(target, files, row, plan.SourceJson[row.Key], plan.TargetJson.GetValueOrDefault(row.Key), cancellationToken)
                    : await this.WriteAsync(target, row, plan.SourceJson[row.Key], plan.TargetJson.GetValueOrDefault(row.Key), cancellationToken);
                failed = error is not null;
                rows.Add(new TransferRow(row, failed ? TransferRowResult.Failed : TransferRowResult.Written, error, null));
            }
        }

        string? sortError = null;
        var sortedParents = 0;
        if (!failed)
        {
            try
            {
                sortedParents = await this.SortAsync(target, source, rows, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException or TransferWriteException)
            {
                LogSortFailed(this.logger, target.Name, exception);
                sortError = "Putting the copied nodes in their place failed: " + exception.Message;
                failed = true;
            }
        }

        // Read every row that wrote or failed back from the target: its state for the report, its last save for the
        // history. A failure is read too, because the target may have saved it anyway (measured: a 500 can mean saved).
        var (finalRows, records) = await this.ReadBackAsync(target, rows, plan.SourceJson);

        string? historyError = null;
        if (records.Count > 0)
        {
            try
            {
                this.history.Record(remote, direction, userKey, records);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogHistoryFailed(this.logger, target.Name, exception);
                historyError = "The copies were made, but recording them in the copy history failed: " + exception.Message;
            }
        }

        var requests = plan.RemoteRequests + target.Requests + files.Requests + source.Requests;
        var written = finalRows.Count(row => row.Result == TransferRowResult.Written);
        LogTransferred(this.logger, direction, remote.Alias, written, failed, sortedParents, requests, stopwatch.ElapsedMilliseconds);
        return new TransferResult(
            failed ? TransferStatus.Failed : TransferStatus.Completed,
            finalRows,
            [],
            sortedParents,
            sortError ?? historyError,
            requests);
    }

    // One create or update. Returns why it failed (null when it worked).
    private async Task<string?> WriteAsync(ITransferTarget target, PlanRow row, string sourceJson, string? targetJson, CancellationToken cancellationToken)
    {
        // A field-level row builds on the target's document as this transfer's own plan just read it.
        var write = row.Fields is null
            ? TransferRequests.For(row, sourceJson)
            : TransferRequests.ForFields(row, sourceJson, targetJson ?? throw new InvalidOperationException($"No target document for the field-level row {row.Key}."));
        var error = await target.SendAsync(write, cancellationToken);
        if (error is not null)
        {
            LogWriteFailed(this.logger, row.Kind, row.Key, target.Name, error);
        }

        return error;
    }

    // One media item: its new or changed files first, each as a temporary file read from the source's media storage,
    // then the create or update naming them, then a read back that every file arrived (Umbraco saves a file it refuses as
    // an empty path and still answers with success, measured). Returns why it failed (null when it worked).
    private async Task<string?> WriteMediaAsync(
        ITransferTarget target,
        IMediaFileSource files,
        PlanRow row,
        string sourceJson,
        string? targetJson,
        CancellationToken cancellationToken)
    {
        var media = MediaTransferRequests.For(row, sourceJson, targetJson, Guid.NewGuid);
        try
        {
            foreach (var upload in media.Uploads)
            {
                await using var file = await files.OpenAsync(upload.Src, cancellationToken);
                if (await target.UploadAsync(upload, file, cancellationToken) is { } uploadError)
                {
                    LogWriteFailed(this.logger, row.Kind, row.Key, target.Name, uploadError);
                    return uploadError;
                }
            }

            if (await target.SendAsync(media.Write, cancellationToken) is { } error)
            {
                LogWriteFailed(this.logger, row.Kind, row.Key, target.Name, error);
                return error;
            }

            if (media.Uploads.Count > 0)
            {
                var missing = MediaTransferRequests.MissingFiles(sourceJson, await target.ReadAsync(PlanItemKind.Media, row.Key, cancellationToken) ?? "{}");
                if (missing.Count > 0)
                {
                    LogFileRefused(this.logger, row.Key, target.Name);
                    return $"It was saved without its file ({string.Join(", ", missing)}): the upload was refused.";
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException or IOException)
        {
            // IOException: the file is missing from the source's media storage.
            LogWriteError(this.logger, row.Kind, row.Key, target.Name, exception);
            return exception.Message;
        }
    }

    // Puts the copied nodes in their place under every parent where the dry run said one moves, from the target's
    // order as it is now, and only when that order changes. The nodes just created are placed as in the dry run, not from
    // where the target put them (last).
    private async Task<int> SortAsync(ITransferTarget target, IPlanSource source, IReadOnlyList<TransferRow> rows, CancellationToken cancellationToken)
    {
        var culture = await this.languageService.GetDefaultIsoCodeAsync();
        var copied = rows
            .Where(row => row.Result is TransferRowResult.Written or TransferRowResult.Unchanged && row.Row.Outcome != PlanOutcome.Conflict)
            .Where(row => row.Row.Notes.All(note => note.Kind != PlanNoteKind.UnderAnotherParentOnTarget))
            .Select(row => row.Row)
            .ToList();
        var created = rows
            .Where(row => row.Result == TransferRowResult.Written && row.Row.Outcome == PlanOutcome.Create)
            .Select(row => row.Row.Key)
            .ToHashSet();
        var parents = copied.Where(row => row.Notes.Any(note => note.Kind == PlanNoteKind.PlacedAfter)).Select(row => row.ParentKey).Distinct();

        var sorted = 0;
        foreach (var parentKey in parents)
        {
            var targetOrder = await target.ReadOrderAsync(parentKey, culture, cancellationToken);
            var sourceOrder = (await source.ReadLevelAsync(parentKey, culture, PlanItemKind.Document, cancellationToken)).Select(child => child.Key).ToList();
            var newOrder = SiblingOrder.PlaceAfterWrite(targetOrder, sourceOrder, copied.Where(row => row.ParentKey == parentKey).Select(row => row.Key).ToHashSet(), created);
            if (newOrder.SequenceEqual(targetOrder))
            {
                continue;
            }

            if (await target.SendAsync(TransferRequests.Sort(parentKey, newOrder), cancellationToken) is { } error)
            {
                throw new TransferWriteException(error);
            }

            sorted++;
        }

        return sorted;
    }

    // Not cancellable on purpose: what was written must reach the copy history even when the request was cancelled.
    // The source's last save comes from the JSON this transfer's own plan read: the source is not written to.
    private async Task<(List<TransferRow> Rows, List<CopyRecord> Records)> ReadBackAsync(
        ITransferTarget target,
        List<TransferRow> rows,
        IReadOnlyDictionary<Guid, string> sourceJson)
    {
        var finalRows = new List<TransferRow>();
        var records = new List<CopyRecord>();
        foreach (var row in rows)
        {
            if (row.Result is not (TransferRowResult.Written or TransferRowResult.Failed))
            {
                finalRows.Add(row);
                continue;
            }

            string? json = null;
            try
            {
                // Null: not on the target, a create that failed before saving anything.
                json = await target.ReadAsync(row.Row.Kind, row.Row.Key, CancellationToken.None);
            }
            catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
            {
                LogReadBackFailed(this.logger, row.Row.Kind, row.Row.Key, target.Name, exception);
            }

            // Media items carry no publish state.
            finalRows.Add(row with { TargetState = json is null ? "absent" : row.Row.Kind == PlanItemKind.Media ? "Saved" : DocumentStamp.States(json) });
            if (row.Result == TransferRowResult.Written && json is not null && DocumentStamp.LastUpdate(json) is { } saved)
            {
                records.Add(new CopyRecord(
                    row.Row.Key,
                    row.Row.Outcome == PlanOutcome.Create ? "Create" : "Update",
                    saved,
                    row.Row.Kind,
                    DocumentStamp.LastUpdate(sourceJson[row.Row.Key])));
            }
        }

        return (finalRows, records);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Transfer {Direction} {Alias} refused: {Rows} rows differ from the dry run")]
    private static partial void LogRefused(ILogger logger, CopyDirection direction, string alias, int rows);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transferred {Direction} {Alias}: {Written} written, failed {Failed}, {Sorted} parents sorted, {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogTransferred(ILogger logger, CopyDirection direction, string alias, int written, bool failed, int sorted, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Writing {Kind} {Key} to {Target} failed: {Error}")]
    private static partial void LogWriteFailed(ILogger logger, PlanItemKind kind, Guid key, string target, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Writing {Kind} {Key} to {Target} failed")]
    private static partial void LogWriteError(ILogger logger, PlanItemKind kind, Guid key, string target, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Target} saved media {Key} without its file: it refused the upload")]
    private static partial void LogFileRefused(ILogger logger, Guid key, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sorting on {Target} failed")]
    private static partial void LogSortFailed(ILogger logger, string target, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading {Kind} {Key} back from {Target} failed")]
    private static partial void LogReadBackFailed(ILogger logger, PlanItemKind kind, Guid key, string target, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording a transfer with {Target} in the copy history failed")]
    private static partial void LogHistoryFailed(ILogger logger, string target, Exception exception);
}