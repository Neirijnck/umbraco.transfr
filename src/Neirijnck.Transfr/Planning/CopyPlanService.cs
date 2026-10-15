using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.References;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Persistence;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// Builds the dry run of copying a selection from here to a remote: expands the selection, reads everything
/// <see cref="CopyPlanner"/> decides on from both sides, and looks up the names the view needs. It only reads.
/// </summary>
public sealed partial class CopyPlanService
{
    /// <summary>Documents per plan: each costs a document request on both sides.</summary>
    public const int MaxDocuments = 2000;

    // Remote document requests in flight at once, as for a tree compare.
    private const int MaxParallelDocumentRequests = 4;

    private readonly LocalTreeReader localTree;
    private readonly RemoteTreeReader remoteTree;
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly SchemaCheckService schemaChecks;
    private readonly LocalSchemaNames schemaNames;
    private readonly CopyHistoryStore history;
    private readonly RemoteAuditReader audit;
    private readonly ILanguageService languageService;
    private readonly IUserService userService;
    private readonly ILogger<CopyPlanService> logger;

    public CopyPlanService(
        LocalTreeReader localTree,
        RemoteTreeReader remoteTree,
        LocalDocumentReader localDocuments,
        RemoteDocumentReader remoteDocuments,
        SchemaCheckService schemaChecks,
        LocalSchemaNames schemaNames,
        CopyHistoryStore history,
        RemoteAuditReader audit,
        ILanguageService languageService,
        IUserService userService,
        ILogger<CopyPlanService> logger)
    {
        this.localTree = localTree;
        this.remoteTree = remoteTree;
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.schemaChecks = schemaChecks;
        this.schemaNames = schemaNames;
        this.history = history;
        this.audit = audit;
        this.languageService = languageService;
        this.userService = userService;
        this.logger = logger;
    }

    /// <exception cref="PlanSelectionException">A selected document doesn't exist here, or the selection is too large.</exception>
    public async Task<CopyPlanResult> PlanAsync(RemoteDefinition remote, IReadOnlyList<PlanSelection> selection, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var culture = await this.languageService.GetDefaultIsoCodeAsync();
        var nodes = this.Expand(selection, culture);

        // Local reads one at a time: Umbraco's ambient scope is not thread-safe.
        var sources = new List<SourceDocument>();
        foreach (var node in nodes)
        {
            var json = await this.localDocuments.ReadAsync(node.Key)
                ?? throw new PlanSelectionException("A selected document could not be read here.", [node.Key]);
            sources.Add(new SourceDocument(node.Key, node.ParentKey, node.Name, json));
        }

        var remoteJson = await this.ReadRemoteDocumentsAsync(remote, sources, cancellationToken);
        var requests = sources.Count;

        var references = sources.SelectMany(source => ReferenceExtractor.Extract(source.Json)).ToList();
        var documentKeys = sources.Select(source => source.Key)
            .Concat(sources.Select(source => source.ParentKey).OfType<Guid>())
            .Concat(references.Where(reference => reference.Kind == ReferenceKind.Document).Select(reference => reference.Key))
            .Distinct()
            .ToList();
        var (onTarget, locateRequests) = await this.remoteTree.LocateDocumentsAsync(remote, documentKeys, cancellationToken);
        requests += locateRequests;

        // Every source's parent, on both sides: a namesake on the target blocks a new node, and the order here is where
        // copied nodes are placed there. A parent that isn't on the target has no children there yet.
        var siblings = new List<TargetSibling>();
        var localSiblings = new List<LocalSibling>();
        foreach (var parentKey in sources.Select(source => source.ParentKey).Distinct())
        {
            localSiblings.AddRange(this.localTree.ReadLevel(parentKey, culture).Select(child => new LocalSibling(parentKey, child.Key)));
            if (parentKey is null || onTarget.ContainsKey(parentKey.Value))
            {
                var (children, readRequests) = await this.remoteTree.ReadLevelAsync(remote, parentKey, culture, cancellationToken);
                requests += readRequests;
                siblings.AddRange(children.Select(child => new TargetSibling(parentKey, child.Key, child.Name)));
            }
        }

        var documentTypes = sources.Select(source => DocumentTypeOf(source.Json)).OfType<Guid>().Distinct().ToList();
        var schema = await this.schemaChecks.CheckAsync(remote, documentTypes, cancellationToken);
        requests += schema.RemoteRequests;

        var mediaKeys = references.Where(reference => reference.Kind == ReferenceKind.Media).Select(reference => reference.Key).Distinct().ToList();
        var localMedia = this.localTree.MediaChains(mediaKeys);
        var (mediaOnTarget, mediaRequests) = mediaKeys.Count == 0
            ? (new Dictionary<Guid, Guid?>(), 0)
            : await this.remoteTree.LocateMediaAsync(remote, mediaKeys.Union(localMedia.Keys).ToList(), cancellationToken);
        requests += mediaRequests;

        var lastCopies = this.history.LatestCopies(remote, sources.Select(source => source.Key).ToList());
        var rows = CopyPlanner.Plan(new CopyPlanFacts
        {
            Sources = sources,
            TargetDocuments = remoteJson.ToDictionary(pair => pair.Key, pair => new TargetDocument(onTarget.GetValueOrDefault(pair.Key)?.ParentKey, pair.Value)),
            DocumentsOnTarget = onTarget.Keys.ToHashSet(),
            PublishedOnTarget = onTarget.Where(pair => pair.Value.IsPublished).Select(pair => pair.Key).ToHashSet(),
            TargetSiblings = siblings,
            LocalSiblings = localSiblings,
            Schema = schema.Checks,
            LocalMedia = localMedia,
            MediaOnTarget = mediaOnTarget.Keys.ToHashSet(),

            // A node different on the target is an update only when the target is as our last copy left it.
            Baseline = lastCopies.ToDictionary(pair => pair.Key, pair => pair.Value.TargetUpdate),
        });

        var conflicts = rows.Where(row => row.Kind == PlanItemKind.Document && row.Outcome == PlanOutcome.Conflict).Select(row => row.Key).ToList();
        var (changes, changeRequests) = await this.ReadRemoteChangesAsync(remote, conflicts, cancellationToken);
        requests += changeRequests;

        var result = this.Describe(rows, siblings, localMedia, schema, culture, requests) with
        {
            SourceJson = sources.ToDictionary(source => source.Key, source => source.Json),
            RemoteChanges = changes,
            LastCopies = await this.DescribeLastCopiesAsync(lastCopies),
        };
        var create = rows.Count(row => row.Outcome == PlanOutcome.Create);
        var conflict = rows.Count(row => row.Outcome == PlanOutcome.Conflict);
        var blocked = rows.Count(row => row.Outcome == PlanOutcome.Blocked);
        LogPlanned(this.logger, remote.Alias, rows.Count, create, conflict, blocked, requests, stopwatch.ElapsedMilliseconds);
        return result;
    }

    // The selected documents and, where asked, everything below them: parents before their children.
    private List<LocalNode> Expand(IReadOnlyList<PlanSelection> selection, string? culture)
    {
        var selected = this.localTree.Describe(selection.Select(choice => choice.Key).Distinct().ToList(), culture);
        var missing = selection.Select(choice => choice.Key).Where(key => !selected.ContainsKey(key)).Distinct().ToList();
        if (missing.Count > 0)
        {
            throw new PlanSelectionException("Some selected documents don't exist here, or are in the recycle bin.", missing);
        }

        var nodes = new List<LocalNode>();
        var seen = new HashSet<Guid>();

        // Shallow choices first, so a parent comes before its children however they were picked.
        foreach (var choice in selection.OrderBy(choice => selected[choice.Key].Level))
        {
            var node = selected[choice.Key];
            if (seen.Add(node.Key))
            {
                nodes.Add(node);
            }

            if (choice.IncludeDescendants)
            {
                this.AddDescendants(node, culture, nodes, seen);
            }

            if (nodes.Count > MaxDocuments)
            {
                throw new PlanSelectionException($"The selection holds more than {MaxDocuments} documents; select less at once.");
            }
        }

        return nodes;
    }

    private void AddDescendants(LocalNode parent, string? culture, List<LocalNode> nodes, HashSet<Guid> seen)
    {
        foreach (var child in this.localTree.ReadLevel(parent.Key, culture))
        {
            var node = new LocalNode(child.Key, parent.Key, child.Name, parent.Level + 1);
            if (seen.Add(node.Key))
            {
                nodes.Add(node);
            }

            if (nodes.Count > MaxDocuments)
            {
                return;
            }

            if (child.HasChildren)
            {
                this.AddDescendants(node, culture, nodes, seen);
            }
        }
    }

    // The remote's copy of every source: absent when it isn't there (404). Any other failure fails the plan.
    private async Task<IReadOnlyDictionary<Guid, string>> ReadRemoteDocumentsAsync(
        RemoteDefinition remote,
        IReadOnlyList<SourceDocument> sources,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<Guid, string>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(sources, options, async (source, token) =>
        {
            try
            {
                found[source.Key] = await this.remoteDocuments.ReadAsync(remote, source.Key, token);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Not on the remote: a node to create.
            }
        });

        return found;
    }

    // Who last changed each conflicting document on the remote. Only an explanation: a read that fails is logged and
    // left out, never failing the plan.
    private async Task<(IReadOnlyDictionary<Guid, RemoteChangeInfo> Changes, int Requests)> ReadRemoteChangesAsync(
        RemoteDefinition remote,
        List<Guid> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return (new Dictionary<Guid, RemoteChangeInfo>(), 0);
        }

        var found = new ConcurrentDictionary<Guid, RemoteChange>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (key, token) =>
        {
            try
            {
                if (await this.audit.LastChangeAsync(remote, key, token) is { } change)
                {
                    found[key] = change;
                }
            }
            catch (Exception exception) when (IsUnreadable(exception, token))
            {
                LogChangeUnread(this.logger, exception, remote.Alias, key);
            }
        });

        var requests = keys.Count;
        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        if (!found.IsEmpty)
        {
            try
            {
                (names, var nameRequests) = await this.audit.UserNamesAsync(remote, found.Values.Select(change => change.UserKey).ToList(), cancellationToken);
                requests += nameRequests;
            }
            catch (Exception exception) when (IsUnreadable(exception, cancellationToken))
            {
                LogUserNamesUnread(this.logger, exception, remote.Alias);
            }
        }

        var changes = found.ToDictionary(
            pair => pair.Key,
            pair => new RemoteChangeInfo(names.GetValueOrDefault(pair.Value.UserKey), pair.Value.At, pair.Value.LogType));
        return (changes, requests);
    }

    // The latest copies with the names of the users here who ran them.
    private async Task<IReadOnlyDictionary<Guid, LastCopyInfo>> DescribeLastCopiesAsync(IReadOnlyDictionary<Guid, LastCopy> lastCopies)
    {
        if (lastCopies.Count == 0)
        {
            return new Dictionary<Guid, LastCopyInfo>();
        }

        var users = await this.userService.GetAsync(lastCopies.Values.Select(copy => copy.UserKey).Distinct());
        var names = users.ToDictionary(user => user.Key, user => user.Name);
        return lastCopies.ToDictionary(pair => pair.Key, pair => new LastCopyInfo(names.GetValueOrDefault(pair.Value.UserKey), pair.Value.CopiedAt));
    }

    // A refused or failed remote read, an unexpected answer, or a timeout; not the caller cancelling.
    private static bool IsUnreadable(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or JsonException or InvalidOperationException or RemoteAuthenticationException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private CopyPlanResult Describe(
        IReadOnlyList<PlanRow> rows,
        IReadOnlyList<TargetSibling> siblings,
        IReadOnlyDictionary<Guid, LocalMediaItem> localMedia,
        SchemaLevelCheck schema,
        string? culture,
        int requests)
    {
        var notes = rows.SelectMany(row => row.Notes).Where(note => note.Key is not null).ToList();
        var documentKeys = notes
            .Where(note => note.Kind is PlanNoteKind.ParentMissingOnTarget or PlanNoteKind.ParentBlocked or PlanNoteKind.ParentNotPublished or PlanNoteKind.MissingReference
                or PlanNoteKind.UnderAnotherParentOnTarget or PlanNoteKind.PlacedAfter)
            .Select(note => note.Key!.Value)
            .Distinct()
            .ToList();
        var documentsHere = this.localTree.Describe(documentKeys, culture);

        var names = new Dictionary<Guid, string>();
        foreach (var (key, node) in documentsHere)
        {
            names[key] = node.Name;
        }

        // A namesake is only on the target; a node to be placed after may be too.
        foreach (var note in notes.Where(note => note.Kind is PlanNoteKind.SameNameOtherKey or PlanNoteKind.PlacedAfter))
        {
            if (siblings.FirstOrDefault(sibling => sibling.Key == note.Key) is { } namesake)
            {
                names.TryAdd(namesake.Key, namesake.Name);
            }
        }

        foreach (var (key, item) in localMedia)
        {
            names.TryAdd(key, item.Name);
        }

        var typeKeys = notes.Where(note => note.Kind is PlanNoteKind.SchemaMismatch or PlanNoteKind.DocumentTypeDiffers).Select(note => note.Key!.Value);
        foreach (var (key, name) in this.schemaNames.DocumentTypeNames(typeKeys))
        {
            names.TryAdd(key, name);
        }

        var documentTypes = new Dictionary<Guid, ContentTypeNames>();
        foreach (var key in rows.Select(row => row.DocumentTypeKey).OfType<Guid>().Distinct())
        {
            if (this.schemaNames.TypeNames(key) is { } typeNames)
            {
                documentTypes[key] = typeNames;
            }
        }

        // Only what a note asks to bring along: a missing parent or a missing referenced document that exists here.
        var selectable = notes
            .Where(note => note.Kind is PlanNoteKind.ParentMissingOnTarget or PlanNoteKind.MissingReference && documentsHere.ContainsKey(note.Key!.Value))
            .Select(note => note.Key!.Value)
            .ToHashSet();

        return new CopyPlanResult(
            rows,
            names,
            selectable,
            documentTypes,
            schema.SeparateInstall,
            requests,
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, RemoteChangeInfo>(),
            new Dictionary<Guid, LastCopyInfo>());
    }

    private static Guid? DocumentTypeOf(string documentJson)
        => Guid.TryParse(JsonNode.Parse(documentJson)?["documentType"]?["id"]?.GetValue<string>(), out var key) ? key : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Planned a copy to remote {Alias}: {Rows} rows ({Create} to create, {Conflict} conflicts, {Blocked} blocked), {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogPlanned(ILogger logger, string alias, int rows, int create, int conflict, int blocked, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read who last changed document {Key} on remote {Alias}")]
    private static partial void LogChangeUnread(ILogger logger, Exception exception, string alias, Guid key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read user names on remote {Alias}")]
    private static partial void LogUserNamesUnread(ILogger logger, Exception exception, string alias);
}