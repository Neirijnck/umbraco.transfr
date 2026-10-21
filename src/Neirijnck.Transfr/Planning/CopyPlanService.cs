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
    /// <summary>Documents and picked media items per plan: each costs a request on both sides.</summary>
    public const int MaxDocuments = 2000;

    // Remote document requests in flight at once, as for a tree compare.
    private const int MaxParallelDocumentRequests = 4;

    private readonly LocalTreeReader localTree;
    private readonly RemoteTreeReader remoteTree;
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly LocalMediaReader localMediaReader;
    private readonly RemoteMediaReader remoteMedia;
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
        LocalMediaReader localMediaReader,
        RemoteMediaReader remoteMedia,
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
        this.localMediaReader = localMediaReader;
        this.remoteMedia = remoteMedia;
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
        var documentChoices = selection.Where(choice => choice.Kind == PlanItemKind.Document).ToList();
        var (nodes, wholeNodes) = this.Expand(documentChoices, culture);
        var pickedMedia = this.ExpandMedia(selection.Where(choice => choice.Kind == PlanItemKind.Media).ToList(), nodes.Count);
        var fields = FieldSelection.Resolve(documentChoices.Select(choice => (choice.Key, choice.Fields)), wholeNodes);

        // Local reads one at a time: Umbraco's ambient scope is not thread-safe.
        var sources = new List<SourceDocument>();
        foreach (var node in nodes)
        {
            var json = await this.localDocuments.ReadAsync(node.Key)
                ?? throw new PlanSelectionException("A selected document could not be read here.", [node.Key]);
            sources.Add(new SourceDocument(node.Key, node.ParentKey, node.Name, json, fields.GetValueOrDefault(node.Key)));
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

        // The media picked and the media the documents reference, and the folders above them: read here one at a time
        // (ambient scope), then located on the target, each wanted one there read whole to compare, and their media types
        // checked.
        var mediaKeys = pickedMedia
            .Concat(references.Where(reference => reference.Kind == ReferenceKind.Media).Select(reference => reference.Key))
            .Distinct()
            .ToList();
        var localMedia = new Dictionary<Guid, LocalMediaItem>();
        foreach (var (key, node) in this.localTree.MediaChains(mediaKeys))
        {
            // Gone since its chain was read: left out, like an unknown item.
            if (await this.localMediaReader.ReadAsync(key) is { } json)
            {
                localMedia[key] = new LocalMediaItem(key, node.ParentKey, node.Name, json);
            }
        }

        var (targetMedia, mediaRequests) = await this.ReadTargetMediaAsync(remote, mediaKeys, localMedia, cancellationToken);
        requests += mediaRequests;
        var mediaTypes = localMedia.Values.Select(item => MediaTypeOf(item.Json)).OfType<Guid>().Distinct().ToList();
        var (mediaSchema, mediaSchemaRequests) = await this.schemaChecks.CheckMediaTypesAsync(remote, mediaTypes, cancellationToken);
        requests += mediaSchemaRequests;

        var lastCopies = this.history.LatestCopies(remote, sources.Select(source => source.Key).Concat(localMedia.Keys).ToList());
        var rows = CopyPlanner.Plan(new CopyPlanFacts
        {
            Sources = sources,
            TargetDocuments = remoteJson.ToDictionary(pair => pair.Key, pair => new TargetDocument(onTarget.GetValueOrDefault(pair.Key)?.ParentKey, pair.Value)),
            DocumentsOnTarget = onTarget.Keys.ToHashSet(),
            PublishedOnTarget = onTarget.Where(pair => pair.Value.IsPublished).Select(pair => pair.Key).ToHashSet(),
            TargetSiblings = siblings,
            LocalSiblings = localSiblings,
            Schema = schema.Checks,
            PickedMedia = pickedMedia,
            LocalMedia = localMedia,
            TargetMedia = targetMedia,
            MediaSchema = mediaSchema,

            // A node or media item different on the target is an update only when the target is as our last copy left it.
            Baseline = lastCopies.ToDictionary(pair => pair.Key, pair => pair.Value.TargetUpdate),
        });

        var conflicts = rows.Where(row => row.Outcome == PlanOutcome.Conflict).Select(row => (row.Kind, row.Key)).ToList();
        var (changes, changeRequests) = await this.ReadRemoteChangesAsync(remote, conflicts, cancellationToken);
        requests += changeRequests;

        var result = this.Describe(rows, siblings, localMedia, schema, culture, requests) with
        {
            SourceJson = sources.ToDictionary(source => source.Key, source => source.Json)
                .Concat(localMedia.ToDictionary(pair => pair.Key, pair => pair.Value.Json))
                .ToDictionary(),
            TargetJson = remoteJson
                .Concat(targetMedia.Where(pair => pair.Value.Json is not null).ToDictionary(pair => pair.Key, pair => pair.Value.Json!))
                .ToDictionary(),
            RemoteChanges = changes,
            LastCopies = await this.DescribeLastCopiesAsync(lastCopies),
        };
        var create = rows.Count(row => row.Outcome == PlanOutcome.Create);
        var conflict = rows.Count(row => row.Outcome == PlanOutcome.Conflict);
        var blocked = rows.Count(row => row.Outcome == PlanOutcome.Blocked);
        LogPlanned(this.logger, remote.Alias, rows.Count, create, conflict, blocked, requests, stopwatch.ElapsedMilliseconds);
        return result;
    }

    // The selected documents and, where asked, everything below them: parents before their children. Also the nodes the
    // selection holds whole (picked without fields, or below a node picked with everything below it).
    private (List<LocalNode> Nodes, HashSet<Guid> WholeNodes) Expand(IReadOnlyList<PlanSelection> selection, string? culture)
    {
        var selected = this.localTree.Describe(selection.Select(choice => choice.Key).Distinct().ToList(), culture);
        var missing = selection.Select(choice => choice.Key).Where(key => !selected.ContainsKey(key)).Distinct().ToList();
        if (missing.Count > 0)
        {
            throw new PlanSelectionException("Some selected documents don't exist here, or are in the recycle bin.", missing);
        }

        var nodes = new List<LocalNode>();
        var seen = new HashSet<Guid>();
        var wholeNodes = new HashSet<Guid>();

        // Shallow choices first, so a parent comes before its children however they were picked.
        foreach (var choice in selection.OrderBy(choice => selected[choice.Key].Level))
        {
            var node = selected[choice.Key];
            if (seen.Add(node.Key))
            {
                nodes.Add(node);
            }

            if (choice.Fields is null)
            {
                wholeNodes.Add(node.Key);
            }

            if (choice.IncludeDescendants)
            {
                var first = nodes.Count;
                this.AddDescendants(node, culture, nodes, seen);
                wholeNodes.UnionWith(nodes.Skip(first).Select(descendant => descendant.Key));
            }

            if (nodes.Count > MaxDocuments)
            {
                throw new PlanSelectionException($"The selection holds more than {MaxDocuments} documents; select less at once.");
            }
        }

        return (nodes, wholeNodes);
    }

    // The media picked for the copy and, where asked, everything below them: parents before their children. Counted with
    // the documents against the plan's limit.
    private List<Guid> ExpandMedia(List<PlanSelection> selection, int documents)
    {
        if (selection.Count == 0)
        {
            return [];
        }

        var here = this.localTree.MediaNames(selection.Select(choice => choice.Key).Distinct().ToList());
        var missing = selection.Select(choice => choice.Key).Where(key => !here.ContainsKey(key)).Distinct().ToList();
        if (missing.Count > 0)
        {
            throw new PlanSelectionException("Some selected media items don't exist here, or are in the recycle bin.", missing);
        }

        var keys = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var choice in selection)
        {
            if (seen.Add(choice.Key))
            {
                keys.Add(choice.Key);
            }

            if (choice.IncludeDescendants)
            {
                this.AddMediaDescendants(choice.Key, keys, seen, documents);
            }

            if (documents + keys.Count > MaxDocuments)
            {
                throw new PlanSelectionException($"The selection holds more than {MaxDocuments} documents and media items; select less at once.");
            }
        }

        return keys;
    }

    private void AddMediaDescendants(Guid parentKey, List<Guid> keys, HashSet<Guid> seen, int documents)
    {
        foreach (var child in this.localTree.ReadLevel(parentKey, null, PlanItemKind.Media))
        {
            if (seen.Add(child.Key))
            {
                keys.Add(child.Key);
            }

            if (documents + keys.Count > MaxDocuments)
            {
                return;
            }

            if (child.HasChildren)
            {
                this.AddMediaDescendants(child.Key, keys, seen, documents);
            }
        }
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

    // The target's copies of the media here: located in item batches (trashed ones included; the referenced keys missing
    // here too, so a note can say that), and each referenced one that is there read whole, to compare. One that is gone
    // between the two reads counts as not there. Any other failure fails the plan, as for documents.
    private async Task<(IReadOnlyDictionary<Guid, TargetMediaItem> Items, int Requests)> ReadTargetMediaAsync(
        RemoteDefinition remote,
        List<Guid> referencedKeys,
        IReadOnlyDictionary<Guid, LocalMediaItem> localMedia,
        CancellationToken cancellationToken)
    {
        if (referencedKeys.Count == 0)
        {
            return (new Dictionary<Guid, TargetMediaItem>(), 0);
        }

        var (located, requests) = await this.remoteTree.LocateMediaAsync(remote, referencedKeys.Union(localMedia.Keys).ToList(), cancellationToken);
        var toRead = referencedKeys.Where(key => localMedia.ContainsKey(key) && located.TryGetValue(key, out var item) && !item.IsTrashed).ToList();
        var found = new ConcurrentDictionary<Guid, string>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(toRead, options, async (key, token) =>
        {
            try
            {
                found[key] = await this.remoteMedia.ReadAsync(remote, key, token);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Gone since it was located: not there.
            }
        });

        var items = new Dictionary<Guid, TargetMediaItem>();
        foreach (var (key, item) in located)
        {
            var json = found.GetValueOrDefault(key);
            if (json is not null || !toRead.Contains(key))
            {
                items[key] = new TargetMediaItem(item.ParentKey, item.IsTrashed, json);
            }
        }

        return (items, requests + toRead.Count);
    }

    // Who last changed each conflicting document or media item on the remote. Only an explanation: a read that fails is
    // logged and left out, never failing the plan.
    private async Task<(IReadOnlyDictionary<Guid, RemoteChangeInfo> Changes, int Requests)> ReadRemoteChangesAsync(
        RemoteDefinition remote,
        List<(PlanItemKind Kind, Guid Key)> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return (new Dictionary<Guid, RemoteChangeInfo>(), 0);
        }

        var found = new ConcurrentDictionary<Guid, RemoteChange>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (item, token) =>
        {
            var (kind, key) = item;
            try
            {
                if (await this.audit.LastChangeAsync(remote, kind, key, token) is { } change)
                {
                    found[key] = change;
                }
            }
            catch (Exception exception) when (IsUnreadable(exception, token))
            {
                LogChangeUnread(this.logger, exception, remote.Alias, kind, key);
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

        var typeKeys = notes.Where(note => note.Kind is PlanNoteKind.SchemaMismatch or PlanNoteKind.DocumentTypeDiffers or PlanNoteKind.MediaTypeDiffers)
            .Select(note => note.Key!.Value)
            .ToList();
        foreach (var (key, name) in this.schemaNames.DocumentTypeNames(typeKeys).Concat(this.schemaNames.MediaTypeNames(typeKeys)))
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
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, RemoteChangeInfo>(),
            new Dictionary<Guid, LastCopyInfo>());
    }

    private static Guid? DocumentTypeOf(string documentJson)
        => Guid.TryParse(JsonNode.Parse(documentJson)?["documentType"]?["id"]?.GetValue<string>(), out var key) ? key : null;

    private static Guid? MediaTypeOf(string mediaJson)
        => Guid.TryParse(JsonNode.Parse(mediaJson)?["mediaType"]?["id"]?.GetValue<string>(), out var key) ? key : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Planned a copy to remote {Alias}: {Rows} rows ({Create} to create, {Conflict} conflicts, {Blocked} blocked), {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogPlanned(ILogger logger, string alias, int rows, int create, int conflict, int blocked, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read who last changed {Kind} {Key} on remote {Alias}")]
    private static partial void LogChangeUnread(ILogger logger, Exception exception, string alias, PlanItemKind kind, Guid key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read user names on remote {Alias}")]
    private static partial void LogUserNamesUnread(ILogger logger, Exception exception, string alias);
}