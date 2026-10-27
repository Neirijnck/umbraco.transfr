using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.History;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.References;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Persistence;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// Builds the dry run of copying a selection between here and a remote, either way: expands the selection on the source,
/// reads everything <see cref="CopyPlanner"/> decides on from both sides, and looks up the names the view needs. It only
/// reads.
/// </summary>
public sealed partial class CopyPlanService
{
    /// <summary>Documents and picked media items per plan: each costs a request on both sides.</summary>
    public const int MaxDocuments = 2000;

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
    private readonly IAuditService auditService;
    private readonly IUserIdKeyResolver userIdKeyResolver;
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
        IAuditService auditService,
        IUserIdKeyResolver userIdKeyResolver,
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
        this.auditService = auditService;
        this.userIdKeyResolver = userIdKeyResolver;
        this.logger = logger;
    }

    /// <param name="remote">The remote copied to or from.</param>
    /// <param name="direction">ToRemote reads the selection here, FromRemote on the remote.</param>
    /// <param name="selection">The documents and media items on the source.</param>
    /// <param name="cancellationToken">Stops the reads.</param>
    /// <exception cref="PlanSelectionException">A selected item doesn't exist on the source, or the selection is too large.</exception>
    public async Task<CopyPlanResult> PlanAsync(
        RemoteDefinition remote,
        CopyDirection direction,
        IReadOnlyList<PlanSelection> selection,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var (source, target) = direction == CopyDirection.FromRemote
            ? ((IPlanSource)new RemotePlanSource(remote, this.remoteTree, this.remoteDocuments, this.remoteMedia),
                (IPlanTarget)new LocalPlanTarget(this.localTree, this.localDocuments, this.localMediaReader, this.auditService, this.userIdKeyResolver, this.userService, this.logger))
            : (new LocalPlanSource(this.localTree, this.localDocuments, this.localMediaReader),
                new RemotePlanTarget(remote, this.remoteTree, this.remoteDocuments, this.remoteMedia, this.audit, this.logger));
        var culture = await this.languageService.GetDefaultIsoCodeAsync();
        var documentChoices = selection.Where(choice => choice.Kind == PlanItemKind.Document).ToList();
        var (expanded, wholeNodes) = await Expand(source, documentChoices, culture, cancellationToken);

        // The planner takes its sources parents first. Here the depth sorts the choices that way already; a remote's
        // item responses carry no depth, so this puts a node picked before its parent after it.
        var nodes = ParentsFirst.Order(expanded, node => node.Key, node => node.ParentKey);
        var pickedMedia = await ExpandMedia(source, selection.Where(choice => choice.Kind == PlanItemKind.Media).ToList(), nodes.Count, cancellationToken);
        var fields = FieldSelection.Resolve(documentChoices.Select(choice => (choice.Key, choice.Fields)), wholeNodes);

        var sourceJson = await source.ReadDocumentsAsync(nodes.Select(node => node.Key).ToList(), cancellationToken);
        var unreadable = nodes.Select(node => node.Key).Where(key => !sourceJson.ContainsKey(key)).ToList();
        if (unreadable.Count > 0)
        {
            throw new PlanSelectionException($"A selected document could not be read {source.Place}.", unreadable);
        }

        var sources = nodes.Select(node => new SourceDocument(node.Key, node.ParentKey, node.Name, sourceJson[node.Key], fields.GetValueOrDefault(node.Key))).ToList();
        var remoteJson = await target.ReadDocumentsAsync(sources.Select(item => item.Key).ToList(), cancellationToken);

        var references = sources.SelectMany(source => ReferenceExtractor.Extract(source.Json)).ToList();
        var documentKeys = sources.Select(source => source.Key)
            .Concat(sources.Select(source => source.ParentKey).OfType<Guid>())
            .Concat(references.Where(reference => reference.Kind == ReferenceKind.Document).Select(reference => reference.Key))
            .Distinct()
            .ToList();
        var onTarget = await target.LocateDocumentsAsync(documentKeys, cancellationToken);

        // Every source's parent, on both sides: a namesake on the target blocks a new node, and the order here is where
        // copied nodes are placed there. A parent that isn't on the target has no children there yet.
        var siblings = new List<TargetSibling>();
        var localSiblings = new List<LocalSibling>();
        foreach (var parentKey in sources.Select(item => item.ParentKey).Distinct())
        {
            var children = await source.ReadLevelAsync(parentKey, culture, PlanItemKind.Document, cancellationToken);
            localSiblings.AddRange(children.Select(child => new LocalSibling(parentKey, child.Key)));
            if (parentKey is null || onTarget.ContainsKey(parentKey.Value))
            {
                var targetChildren = await target.ReadLevelAsync(parentKey, culture, cancellationToken);
                siblings.AddRange(targetChildren.Select(child => new TargetSibling(parentKey, child.Key, child.Name)));
            }
        }

        var documentTypes = sources.Select(source => DocumentTypeOf(source.Json)).OfType<Guid>().Distinct().ToList();
        var schema = await this.schemaChecks.CheckAsync(remote, documentTypes, cancellationToken);
        var requests = schema.RemoteRequests;

        // The media picked and the media the documents reference, and the folders above them: read on the source, then
        // located on the target, each wanted one there read whole to compare, and their media types checked.
        var mediaKeys = pickedMedia
            .Concat(references.Where(reference => reference.Kind == ReferenceKind.Media).Select(reference => reference.Key))
            .Distinct()
            .ToList();
        var chains = await source.MediaChainsAsync(mediaKeys, cancellationToken);
        var mediaJson = await source.ReadMediaAsync(chains.Keys.ToList(), cancellationToken);

        // Gone since its chain was read: left out, like an unknown item.
        var localMedia = chains
            .Where(pair => mediaJson.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => new LocalMediaItem(pair.Key, pair.Value.ParentKey, pair.Value.Name, mediaJson[pair.Key]));

        var targetMedia = await ReadTargetMediaAsync(target, mediaKeys, localMedia, cancellationToken);
        var mediaTypes = localMedia.Values.Select(item => MediaTypeOf(item.Json)).OfType<Guid>().Distinct().ToList();
        var (mediaSchema, mediaSchemaRequests) = await this.schemaChecks.CheckMediaTypesAsync(remote, mediaTypes, cancellationToken);
        requests += mediaSchemaRequests;

        var lastCopies = this.history.LatestCopies(remote, sources.Select(item => item.Key).Concat(localMedia.Keys).ToList(), direction);
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

            // A node or media item different on the target is an update only when the target is as our last copy, either
            // way, left it; without that side recorded there is no baseline (a conflict).
            Baseline = lastCopies
                .Where(pair => pair.Value.TargetUpdate is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value.TargetUpdate!.Value),
        });

        var conflicts = rows.Where(row => row.Outcome == PlanOutcome.Conflict).Select(row => (row.Kind, row.Key)).ToList();
        var changes = await target.LastChangesAsync(conflicts, cancellationToken);
        requests += source.Requests + target.Requests;

        var result = await this.Describe(source, rows, siblings, localMedia, schema, culture, requests, cancellationToken) with
        {
            SourceJson = sources.ToDictionary(item => item.Key, item => item.Json)
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
        LogPlanned(this.logger, direction, remote.Alias, rows.Count, create, conflict, blocked, requests, stopwatch.ElapsedMilliseconds);
        return result;
    }

    // The selected documents and, where asked, everything below them: parents before their children. Also the nodes the
    // selection holds whole (picked without fields, or below a node picked with everything below it).
    private static async Task<(List<LocalNode> Nodes, HashSet<Guid> WholeNodes)> Expand(
        IPlanSource source,
        IReadOnlyList<PlanSelection> selection,
        string? culture,
        CancellationToken cancellationToken)
    {
        var selected = await source.DescribeDocumentsAsync(selection.Select(choice => choice.Key).Distinct().ToList(), culture, cancellationToken);
        var missing = selection.Select(choice => choice.Key).Where(key => !selected.ContainsKey(key)).Distinct().ToList();
        if (missing.Count > 0)
        {
            throw new PlanSelectionException($"Some selected documents don't exist {source.Place}, or are in the recycle bin.", missing);
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
                await AddDescendants(source, node, culture, nodes, seen, cancellationToken);
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
    private static async Task<List<Guid>> ExpandMedia(IPlanSource source, List<PlanSelection> selection, int documents, CancellationToken cancellationToken)
    {
        if (selection.Count == 0)
        {
            return [];
        }

        var here = await source.MediaNamesAsync(selection.Select(choice => choice.Key).Distinct().ToList(), cancellationToken);
        var missing = selection.Select(choice => choice.Key).Where(key => !here.ContainsKey(key)).Distinct().ToList();
        if (missing.Count > 0)
        {
            throw new PlanSelectionException($"Some selected media items don't exist {source.Place}, or are in the recycle bin.", missing);
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
                await AddMediaDescendants(source, choice.Key, keys, seen, documents, cancellationToken);
            }

            if (documents + keys.Count > MaxDocuments)
            {
                throw new PlanSelectionException($"The selection holds more than {MaxDocuments} documents and media items; select less at once.");
            }
        }

        return keys;
    }

    private static async Task AddMediaDescendants(IPlanSource source, Guid parentKey, List<Guid> keys, HashSet<Guid> seen, int documents, CancellationToken cancellationToken)
    {
        foreach (var child in await source.ReadLevelAsync(parentKey, null, PlanItemKind.Media, cancellationToken))
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
                await AddMediaDescendants(source, child.Key, keys, seen, documents, cancellationToken);
            }
        }
    }

    private static async Task AddDescendants(IPlanSource source, LocalNode parent, string? culture, List<LocalNode> nodes, HashSet<Guid> seen, CancellationToken cancellationToken)
    {
        foreach (var child in await source.ReadLevelAsync(parent.Key, culture, PlanItemKind.Document, cancellationToken))
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
                await AddDescendants(source, node, culture, nodes, seen, cancellationToken);
            }
        }
    }

    // The target's copies of the media on the source: located in item batches (trashed ones included; the referenced keys
    // missing on the source too, so a note can say that), and each referenced one that is there read whole, to compare.
    // One that is gone between the two reads counts as not there. Any other failure fails the plan, as for documents.
    private static async Task<IReadOnlyDictionary<Guid, TargetMediaItem>> ReadTargetMediaAsync(
        IPlanTarget target,
        List<Guid> referencedKeys,
        IReadOnlyDictionary<Guid, LocalMediaItem> localMedia,
        CancellationToken cancellationToken)
    {
        if (referencedKeys.Count == 0)
        {
            return new Dictionary<Guid, TargetMediaItem>();
        }

        var located = await target.LocateMediaAsync(referencedKeys.Union(localMedia.Keys).ToList(), cancellationToken);
        var toRead = referencedKeys.Where(key => localMedia.ContainsKey(key) && located.TryGetValue(key, out var item) && !item.IsTrashed).ToList();
        var found = await target.ReadMediaAsync(toRead, cancellationToken);

        var items = new Dictionary<Guid, TargetMediaItem>();
        foreach (var (key, item) in located)
        {
            var json = found.GetValueOrDefault(key);
            if (json is not null || !toRead.Contains(key))
            {
                items[key] = new TargetMediaItem(item.ParentKey, item.IsTrashed, json);
            }
        }

        return items;
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

    private async Task<CopyPlanResult> Describe(
        IPlanSource source,
        IReadOnlyList<PlanRow> rows,
        IReadOnlyList<TargetSibling> siblings,
        IReadOnlyDictionary<Guid, LocalMediaItem> localMedia,
        SchemaLevelCheck schema,
        string? culture,
        int requests,
        CancellationToken cancellationToken)
    {
        var notes = rows.SelectMany(row => row.Notes).Where(note => note.Key is not null).ToList();
        var documentKeys = notes
            .Where(note => note.Kind is PlanNoteKind.ParentMissingOnTarget or PlanNoteKind.ParentBlocked or PlanNoteKind.ParentNotPublished or PlanNoteKind.MissingReference
                or PlanNoteKind.UnderAnotherParentOnTarget or PlanNoteKind.PlacedAfter)
            .Select(note => note.Key!.Value)
            .Distinct()
            .ToList();
        var documentsHere = await source.DescribeDocumentsAsync(documentKeys, culture, cancellationToken);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Planned a copy {Direction} {Alias}: {Rows} rows ({Create} to create, {Conflict} conflicts, {Blocked} blocked), {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogPlanned(ILogger logger, CopyDirection direction, string alias, int rows, int create, int conflict, int blocked, int requests, long milliseconds);

}