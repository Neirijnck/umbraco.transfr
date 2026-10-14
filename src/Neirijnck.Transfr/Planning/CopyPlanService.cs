using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.References;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
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
    private readonly ILanguageService languageService;
    private readonly ILogger<CopyPlanService> logger;

    public CopyPlanService(
        LocalTreeReader localTree,
        RemoteTreeReader remoteTree,
        LocalDocumentReader localDocuments,
        RemoteDocumentReader remoteDocuments,
        SchemaCheckService schemaChecks,
        LocalSchemaNames schemaNames,
        ILanguageService languageService,
        ILogger<CopyPlanService> logger)
    {
        this.localTree = localTree;
        this.remoteTree = remoteTree;
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.schemaChecks = schemaChecks;
        this.schemaNames = schemaNames;
        this.languageService = languageService;
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
        var (onTarget, locateRequests) = await this.remoteTree.LocateAsync(remote, documentKeys, cancellationToken);
        requests += locateRequests;

        // The target's children of every parent a new node would be created under: a namesake there blocks it.
        var siblings = new List<TargetSibling>();
        var newNodeParents = sources
            .Where(source => !remoteJson.ContainsKey(source.Key) && (source.ParentKey is null || onTarget.ContainsKey(source.ParentKey.Value)))
            .Select(source => source.ParentKey)
            .Distinct();
        foreach (var parentKey in newNodeParents)
        {
            var (children, readRequests) = await this.remoteTree.ReadLevelAsync(remote, parentKey, culture, cancellationToken);
            requests += readRequests;
            siblings.AddRange(children.Select(child => new TargetSibling(parentKey, child.Key, child.Name)));
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

        var rows = CopyPlanner.Plan(new CopyPlanFacts
        {
            Sources = sources,
            TargetDocuments = remoteJson.ToDictionary(pair => pair.Key, pair => new TargetDocument(onTarget.GetValueOrDefault(pair.Key), pair.Value)),
            DocumentsOnTarget = onTarget.Keys.ToHashSet(),
            TargetSiblings = siblings,
            Schema = schema.Checks,
            LocalMedia = localMedia,
            MediaOnTarget = mediaOnTarget.Keys.ToHashSet(),

            // No history yet (step 9 records copies), so every different node is a conflict.
            Baseline = new Dictionary<Guid, DateTimeOffset>(),
        });

        var result = this.Describe(rows, siblings, localMedia, schema, culture, requests);
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
            .Where(note => note.Kind is PlanNoteKind.ParentMissingOnTarget or PlanNoteKind.ParentBlocked or PlanNoteKind.MissingReference
                or PlanNoteKind.UnderAnotherParentOnTarget)
            .Select(note => note.Key!.Value)
            .Distinct()
            .ToList();
        var documentsHere = this.localTree.Describe(documentKeys, culture);

        var names = new Dictionary<Guid, string>();
        foreach (var (key, node) in documentsHere)
        {
            names[key] = node.Name;
        }

        foreach (var note in notes.Where(note => note.Kind == PlanNoteKind.SameNameOtherKey))
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

        return new CopyPlanResult(rows, names, selectable, documentTypes, schema.SeparateInstall, requests);
    }

    private static Guid? DocumentTypeOf(string documentJson)
        => Guid.TryParse(JsonNode.Parse(documentJson)?["documentType"]?["id"]?.GetValue<string>(), out var key) ? key : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Planned a copy to remote {Alias}: {Rows} rows ({Create} to create, {Conflict} conflicts, {Blocked} blocked), {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogPlanned(ILogger logger, string alias, int rows, int create, int conflict, int blocked, int requests, long milliseconds);
}