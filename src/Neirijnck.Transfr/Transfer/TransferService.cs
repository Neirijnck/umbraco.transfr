using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
/// Carries out a dry run on a remote: plans again and refuses when anything differs from what the dry run showed,
/// writes row by row (media before the documents that use them, parents first) and stops at the first failure, puts
/// copied nodes in their place among their siblings, and records what it wrote in the copy history. It writes only
/// through the allowlisted upload, create, update and sort requests: nothing is deleted, unpublished or moved.
/// </summary>
public sealed partial class TransferService
{
    private static readonly string[] ProblemFields = ["title", "detail"];

    // One transfer per remote environment at a time: two at once would each plan against a remote the other changes.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> running = new(StringComparer.Ordinal);

    private readonly CopyPlanService plans;
    private readonly RemoteClient client;
    private readonly RemoteTreeReader remoteTree;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly RemoteMediaReader remoteMedia;
    private readonly MediaFileManager mediaFiles;
    private readonly LocalTreeReader localTree;
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
        this.history = history;
        this.languageService = languageService;
        this.logger = logger;
    }

    /// <param name="remote">The remote to write to.</param>
    /// <param name="userKey">The backoffice user, for the copy history.</param>
    /// <param name="selection">The selection the dry run was made for.</param>
    /// <param name="dryRun">The rows as the dry run showed them.</param>
    /// <param name="overwrite">Conflicts chosen to overwrite.</param>
    /// <param name="cancellationToken">Stops between rows; a write already sent is not undone.</param>
    /// <exception cref="PlanSelectionException">The selection can't be planned, or overwrite names a node that isn't a conflict.</exception>
    public async Task<TransferResult> TransferAsync(
        RemoteDefinition remote,
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
            return new TransferResult(TransferStatus.Busy, [], [], 0, "Another transfer to this remote is running.", 0);
        }

        try
        {
            return await this.RunAsync(remote, userKey, selection, dryRun, overwrite, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TransferResult> RunAsync(
        RemoteDefinition remote,
        Guid userKey,
        IReadOnlyList<PlanSelection> selection,
        IReadOnlyList<DryRunRow> dryRun,
        IReadOnlySet<Guid> overwrite,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var plan = await this.plans.PlanAsync(remote, CopyDirection.ToRemote, selection, cancellationToken);
        var requests = plan.RemoteRequests;

        var drift = PlanCheck.Compare(plan.Rows, dryRun);
        if (drift.Count > 0)
        {
            LogRefused(this.logger, remote.Alias, drift.Count);
            return new TransferResult(TransferStatus.Refused, [], drift, 0, null, requests);
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
                var (error, extraRequests) = row.Kind == PlanItemKind.Media
                    ? await this.WriteMediaAsync(remote, row, plan.SourceJson[row.Key], plan.TargetJson.GetValueOrDefault(row.Key), cancellationToken)
                    : await this.WriteAsync(remote, row, plan.SourceJson[row.Key], plan.TargetJson.GetValueOrDefault(row.Key), cancellationToken);
                requests += extraRequests;
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
                (sortedParents, var sortRequests) = await this.SortAsync(remote, rows, cancellationToken);
                requests += sortRequests;
            }
            catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
            {
                LogSortFailed(this.logger, remote.Alias, exception);
                sortError = "Putting the copied nodes in their place failed: " + exception.Message;
                failed = true;
            }
        }

        // Read every row that wrote or failed back from the remote: its state for the report, its last save for the
        // history. A failure is read too, because the remote may have saved it anyway (measured: a 500 can mean saved).
        var (finalRows, readRequests, records) = await this.ReadBackAsync(remote, rows, plan.SourceJson);
        requests += readRequests;

        string? historyError = null;
        if (records.Count > 0)
        {
            try
            {
                this.history.Record(remote, CopyDirection.ToRemote, userKey, records);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogHistoryFailed(this.logger, remote.Alias, exception);
                historyError = "The copies were made, but recording them in the copy history failed: " + exception.Message;
            }
        }

        var written = finalRows.Count(row => row.Result == TransferRowResult.Written);
        LogTransferred(this.logger, remote.Alias, written, failed, sortedParents, requests, stopwatch.ElapsedMilliseconds);
        return new TransferResult(
            failed ? TransferStatus.Failed : TransferStatus.Completed,
            finalRows,
            [],
            sortedParents,
            sortError ?? historyError,
            requests);
    }

    // One create or update. Returns why it failed (null when it worked) and the requests it cost.
    private async Task<(string? Error, int Requests)> WriteAsync(
        RemoteDefinition remote,
        PlanRow row,
        string sourceJson,
        string? targetJson,
        CancellationToken cancellationToken)
    {
        // A field-level row builds on the target's document as this transfer's own plan just read it.
        var write = row.Fields is null
            ? TransferRequests.For(row, sourceJson)
            : TransferRequests.ForFields(row, sourceJson, targetJson ?? throw new InvalidOperationException($"No remote document for the field-level row {row.Key}."));
        try
        {
            using var content = new StringContent(write.Body, Encoding.UTF8, "application/json");
            using var response = await this.client.SendAsync(remote, new HttpMethod(write.Method), write.Path, content: content, cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (null, 1);
            }

            var error = $"The remote answered {(int)response.StatusCode}: {ProblemOf(await response.Content.ReadAsStringAsync(cancellationToken))}";
            LogWriteFailed(this.logger, row.Kind, row.Key, remote.Alias, (int)response.StatusCode);
            return (error, 1);
        }
        catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
        {
            LogWriteError(this.logger, row.Kind, row.Key, remote.Alias, exception);
            return (exception.Message, 1);
        }
    }

    // One media item: its new or changed files first, each as a temporary file read from this site's media storage, then
    // the create or update naming them, then a read back that every file arrived (Umbraco saves a file it refuses as an
    // empty path and still answers with success, measured). Returns why it failed (null when it worked) and the requests.
    private async Task<(string? Error, int Requests)> WriteMediaAsync(
        RemoteDefinition remote,
        PlanRow row,
        string sourceJson,
        string? targetJson,
        CancellationToken cancellationToken)
    {
        var media = MediaTransferRequests.For(row, sourceJson, targetJson, Guid.NewGuid);
        var requests = 0;
        try
        {
            foreach (var upload in media.Uploads)
            {
                await using var file = this.mediaFiles.FileSystem.OpenFile(this.mediaFiles.FileSystem.GetRelativePath(upload.Src));
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(upload.TemporaryFileId.ToString("D")), "Id");
                var fileContent = new StreamContent(file);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "File", upload.FileName);
                using var uploaded = await this.client.SendAsync(remote, HttpMethod.Post, $"{RemoteRequestPolicy.ApiBase}/temporary-file", content: form, cancellationToken: cancellationToken);
                requests++;
                if (!uploaded.IsSuccessStatusCode)
                {
                    LogWriteFailed(this.logger, row.Kind, row.Key, remote.Alias, (int)uploaded.StatusCode);
                    return ($"Uploading {upload.FileName} failed: the remote answered {(int)uploaded.StatusCode}: {ProblemOf(await uploaded.Content.ReadAsStringAsync(cancellationToken))}", requests);
                }
            }

            using var content = new StringContent(media.Write.Body, Encoding.UTF8, "application/json");
            using var response = await this.client.SendAsync(remote, new HttpMethod(media.Write.Method), media.Write.Path, content: content, cancellationToken: cancellationToken);
            requests++;
            if (!response.IsSuccessStatusCode)
            {
                LogWriteFailed(this.logger, row.Kind, row.Key, remote.Alias, (int)response.StatusCode);
                return ($"The remote answered {(int)response.StatusCode}: {ProblemOf(await response.Content.ReadAsStringAsync(cancellationToken))}", requests);
            }

            if (media.Uploads.Count > 0)
            {
                var missing = MediaTransferRequests.MissingFiles(sourceJson, await this.remoteMedia.ReadAsync(remote, row.Key, cancellationToken));
                requests++;
                if (missing.Count > 0)
                {
                    LogFileRefused(this.logger, row.Key, remote.Alias);
                    return ($"The remote saved it without its file ({string.Join(", ", missing)}): it refused the upload.", requests);
                }
            }

            return (null, requests);
        }
        catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException or IOException)
        {
            // IOException: the file is missing from this site's media storage.
            LogWriteError(this.logger, row.Kind, row.Key, remote.Alias, exception);
            return (exception.Message, requests);
        }
    }

    // Puts the copied nodes in their place under every parent where the dry run said one moves, from the remote's
    // order as it is now, and only when that order changes. The nodes just created are placed as in the dry run, not from
    // where the remote put them (last).
    private async Task<(int Parents, int Requests)> SortAsync(RemoteDefinition remote, IReadOnlyList<TransferRow> rows, CancellationToken cancellationToken)
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
        var requests = 0;
        foreach (var parentKey in parents)
        {
            var (children, readRequests) = await this.remoteTree.ReadLevelAsync(remote, parentKey, culture, cancellationToken);
            requests += readRequests;
            var targetOrder = children.Select(child => child.Key).ToList();
            var localOrder = this.localTree.ReadLevel(parentKey, culture).Select(child => child.Key).ToList();
            var newOrder = SiblingOrder.PlaceAfterWrite(targetOrder, localOrder, copied.Where(row => row.ParentKey == parentKey).Select(row => row.Key).ToHashSet(), created);
            if (newOrder.SequenceEqual(targetOrder))
            {
                continue;
            }

            var sort = TransferRequests.Sort(parentKey, newOrder);
            using var content = new StringContent(sort.Body, Encoding.UTF8, "application/json");
            using var response = await this.client.SendAsync(remote, new HttpMethod(sort.Method), sort.Path, content: content, cancellationToken: cancellationToken);
            requests++;
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"The remote answered {(int)response.StatusCode} to the sort: {ProblemOf(await response.Content.ReadAsStringAsync(cancellationToken))}",
                    null,
                    response.StatusCode);
            }

            sorted++;
        }

        return (sorted, requests);
    }

    // Not cancellable on purpose: what was written must reach the copy history even when the request was cancelled.
    // The source's last save comes from the JSON this transfer's own plan read: the source is not written to.
    private async Task<(List<TransferRow> Rows, int Requests, List<CopyRecord> Records)> ReadBackAsync(
        RemoteDefinition remote,
        List<TransferRow> rows,
        IReadOnlyDictionary<Guid, string> sourceJson)
    {
        var finalRows = new List<TransferRow>();
        var records = new List<CopyRecord>();
        var requests = 0;
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
                json = row.Row.Kind == PlanItemKind.Media
                    ? await this.remoteMedia.ReadAsync(remote, row.Row.Key, CancellationToken.None)
                    : await this.remoteDocuments.ReadAsync(remote, row.Row.Key, CancellationToken.None);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Not on the remote: a create that failed before saving anything.
            }
            catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
            {
                LogReadBackFailed(this.logger, row.Row.Kind, row.Row.Key, remote.Alias, exception);
            }

            requests++;
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

        return (finalRows, requests, records);
    }

    // The title and detail of a ProblemDetails body, or the body itself (shortened) when it isn't one.
    private static string ProblemOf(string body)
    {
        try
        {
            using var problem = JsonDocument.Parse(body);
            var parts = ProblemFields
                .Select(name => problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null)
                .Where(part => !string.IsNullOrWhiteSpace(part));
            var text = string.Join(": ", parts);
            if (text.Length > 0)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Not JSON: the body as it came, below.
        }

        return body.Length > 200 ? body[..200] + "…" : body;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Transfer to remote {Alias} refused: {Rows} rows differ from the dry run")]
    private static partial void LogRefused(ILogger logger, string alias, int rows);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transferred to remote {Alias}: {Written} written, failed {Failed}, {Sorted} parents sorted, {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogTransferred(ILogger logger, string alias, int written, bool failed, int sorted, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Writing {Kind} {Key} to remote {Alias} failed with {Status}")]
    private static partial void LogWriteFailed(ILogger logger, PlanItemKind kind, Guid key, string alias, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Writing {Kind} {Key} to remote {Alias} failed")]
    private static partial void LogWriteError(ILogger logger, PlanItemKind kind, Guid key, string alias, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote {Alias} saved media {Key} without its file: it refused the upload")]
    private static partial void LogFileRefused(ILogger logger, Guid key, string alias);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sorting on remote {Alias} failed")]
    private static partial void LogSortFailed(ILogger logger, string alias, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading {Kind} {Key} back from remote {Alias} failed")]
    private static partial void LogReadBackFailed(ILogger logger, PlanItemKind kind, Guid key, string alias, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording a transfer to remote {Alias} in the copy history failed")]
    private static partial void LogHistoryFailed(ILogger logger, string alias, Exception exception);
}