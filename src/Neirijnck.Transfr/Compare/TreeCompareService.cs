using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Schema;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Compares one tree level, of the content tree or the media tree: reads it on both sides, looks up the keys found on
/// one side only (one batch per side), classifies every node with <see cref="TreeLevelComparer"/>, and compares every
/// node that exists on both sides (<see cref="ContentComparer"/>, <see cref="MediaComparer"/>).
/// </summary>
public sealed partial class TreeCompareService
{
    // Remote document requests in flight at once: one level of a large site stays quick without flooding the remote.
    private const int MaxParallelDocumentRequests = 4;

    // Nodes per subtree compare: each costs a document request on both sides, so this bounds one request's duration.
    private const int MaxSubtreeNodes = 2000;

    private readonly LocalTreeReader localTree;
    private readonly RemoteTreeReader remoteTree;
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly LocalMediaReader localMedia;
    private readonly RemoteMediaReader remoteMedia;
    private readonly SchemaCheckService schemaChecks;
    private readonly LocalSchemaNames schemaNames;
    private readonly ILanguageService languageService;
    private readonly ILogger<TreeCompareService> logger;

    public TreeCompareService(
        LocalTreeReader localTree,
        RemoteTreeReader remoteTree,
        LocalDocumentReader localDocuments,
        RemoteDocumentReader remoteDocuments,
        LocalMediaReader localMedia,
        RemoteMediaReader remoteMedia,
        SchemaCheckService schemaChecks,
        LocalSchemaNames schemaNames,
        ILanguageService languageService,
        ILogger<TreeCompareService> logger)
    {
        this.localTree = localTree;
        this.remoteTree = remoteTree;
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.localMedia = localMedia;
        this.remoteMedia = remoteMedia;
        this.schemaChecks = schemaChecks;
        this.schemaNames = schemaNames;
        this.languageService = languageService;
        this.logger = logger;
    }

    /// <summary>The children of a document (null = the root) on both sides.</summary>
    public Task<TreeLevelComparison> CompareLevelAsync(RemoteDefinition remote, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareLevelAsync(remote, parentKey, PlanItemKind.Document, cancellationToken);

    /// <summary>The children of a media folder (null = the root) on both sides.</summary>
    public Task<TreeLevelComparison> CompareMediaLevelAsync(RemoteDefinition remote, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareLevelAsync(remote, parentKey, PlanItemKind.Media, cancellationToken);

    /// <summary>
    /// Compares every level below a document (null = the whole tree), as <see cref="SubtreeWalker"/> walks it.
    /// </summary>
    public Task<SubtreeComparison> CompareSubtreeAsync(RemoteDefinition remote, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareSubtreeAsync(remote, parentKey, PlanItemKind.Document, cancellationToken);

    /// <summary>
    /// Compares every level below a media folder (null = the whole media tree), as <see cref="SubtreeWalker"/> walks it.
    /// </summary>
    public Task<SubtreeComparison> CompareMediaSubtreeAsync(RemoteDefinition remote, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareSubtreeAsync(remote, parentKey, PlanItemKind.Media, cancellationToken);

    /// <summary>
    /// The document type a row's node would be copied with (a media item's media type): its local type, or the
    /// remote's for a node only there.
    /// </summary>
    public static Guid? DocumentTypeOf(TreeCompareRow row) => (row.Local ?? row.Remote)?.DocumentTypeKey;

    private async Task<TreeLevelComparison> CompareLevelAsync(RemoteDefinition remote, Guid? parentKey, PlanItemKind kind, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Names are shown in this environment's default language; the remote's names in the same culture.
        var culture = await this.languageService.GetDefaultIsoCodeAsync();

        var local = this.localTree.ReadLevel(parentKey, culture, kind);
        var (remoteNodes, readRequests) = await this.remoteTree.ReadLevelAsync(remote, parentKey, culture, cancellationToken, kind);

        var (localOnly, remoteOnly) = TreeLevelComparer.KeysToLocate(local, remoteNodes);
        var (remoteParents, locateRequests) = await this.LocateOnRemoteAsync(remote, localOnly.ToList(), kind, cancellationToken);
        var localParents = this.localTree.Locate(remoteOnly.ToList(), kind);

        var rows = TreeLevelComparer.Compare(local, remoteNodes, remoteParents, localParents);

        // A node under another parent on one side still exists on both: its content is compared too.
        var bothSides = rows.Where(row => row.Status != TreeCompareStatus.OnlyLocal && row.Status != TreeCompareStatus.OnlyRemote)
            .Select(row => row.Key)
            .ToList();
        var contents = await this.CompareContentsAsync(remote, bothSides, kind, cancellationToken);

        // The schema each node would be copied with: its local type, or the remote's for a node only there. A separate
        // installation shows in the document types; built-in media types share their keys (measured).
        var types = rows.Select(DocumentTypeOf).OfType<Guid>().Distinct().ToList();
        SchemaLevelCheck schema;
        if (kind == PlanItemKind.Media)
        {
            var (checks, schemaRequests) = await this.schemaChecks.CheckMediaTypesAsync(remote, types, cancellationToken);
            schema = new SchemaLevelCheck(checks, [], schemaRequests);
        }
        else
        {
            schema = await this.schemaChecks.CheckAsync(remote, types, cancellationToken);
        }

        // Names for the type icons on both sides: this environment's, else the remote's from the check.
        var sideTypes = rows.SelectMany(row => new[] { row.Local?.DocumentTypeKey, row.Remote?.DocumentTypeKey }).OfType<Guid>();
        var typeNames = new Dictionary<Guid, string>(kind == PlanItemKind.Media
            ? this.schemaNames.MediaTypeNames(sideTypes)
            : this.schemaNames.DocumentTypeNames(sideTypes));
        var typeKind = kind == PlanItemKind.Media ? SchemaItemKind.MediaType : SchemaItemKind.DocumentType;
        foreach (var problem in schema.Checks.Values.SelectMany(check => check.Problems))
        {
            if (problem.Kind == typeKind && problem.Name is not null)
            {
                typeNames.TryAdd(problem.Key, problem.Name);
            }
        }

        var requests = readRequests + locateRequests + bothSides.Count + schema.RemoteRequests;
        var different = contents.Values.Count(content => content.Comparison is { IsIdentical: false });
        var failed = contents.Values.Count(content => content.Error is not null);
        var blocked = schema.Checks.Count(check => !check.Value.IsOk);

        LogCompared(this.logger, kind, parentKey, remote.Alias, rows.Count, contents.Count, different, failed, blocked, requests, stopwatch.ElapsedMilliseconds);
        return new TreeLevelComparison(rows, contents, schema, typeNames, requests);
    }

    private async Task<SubtreeComparison> CompareSubtreeAsync(RemoteDefinition remote, Guid? parentKey, PlanItemKind kind, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var (levels, truncated) = await SubtreeWalker.WalkAsync(
            parentKey,
            (levelKey, token) => this.CompareLevelAsync(remote, levelKey, kind, token),
            level => level.Rows,
            MaxSubtreeNodes,
            cancellationToken);

        var requests = levels.Sum(level => level.Level.RemoteRequests);
        var nodes = levels.Sum(level => level.Level.Rows.Count);
        LogSubtreeCompared(this.logger, kind, parentKey, remote.Alias, levels.Count, nodes, requests, truncated, stopwatch.ElapsedMilliseconds);
        return new SubtreeComparison(levels, requests, truncated);
    }

    // Where the remote has these keys, outside its recycle bin: key to parent (null = the root).
    private async Task<(IReadOnlyDictionary<Guid, Guid?> Parents, int Requests)> LocateOnRemoteAsync(
        RemoteDefinition remote,
        List<Guid> keys,
        PlanItemKind kind,
        CancellationToken cancellationToken)
    {
        if (kind == PlanItemKind.Document)
        {
            return await this.remoteTree.LocateAsync(remote, keys, cancellationToken);
        }

        var (items, requests) = await this.remoteTree.LocateMediaAsync(remote, keys, cancellationToken);
        return (items.Where(pair => !pair.Value.IsTrashed).ToDictionary(pair => pair.Key, pair => pair.Value.ParentKey), requests);
    }

    private async Task<IReadOnlyDictionary<Guid, NodeContentComparison>> CompareContentsAsync(
        RemoteDefinition remote,
        IReadOnlyList<Guid> keys,
        PlanItemKind kind,
        CancellationToken cancellationToken)
    {
        // Local reads one at a time: Umbraco's ambient scope is not thread-safe.
        var localJson = new Dictionary<Guid, string?>();
        foreach (var key in keys)
        {
            localJson[key] = kind == PlanItemKind.Media ? await this.localMedia.ReadAsync(key) : await this.localDocuments.ReadAsync(key);
        }

        var remoteJson = new ConcurrentDictionary<Guid, (string? Json, string? Error)>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (key, token) =>
        {
            try
            {
                var json = kind == PlanItemKind.Media
                    ? await this.remoteMedia.ReadAsync(remote, key, token)
                    : await this.remoteDocuments.ReadAsync(remote, key, token);
                remoteJson[key] = (json, null);
            }
            catch (HttpRequestException exception)
            {
                // One unreadable item fails its own row, not the level. A refused sign-in still fails the level.
                LogItemFailed(this.logger, kind, key, remote.Alias, exception);
                remoteJson[key] = (null, exception.Message);
            }
        });

        var contents = new Dictionary<Guid, NodeContentComparison>();
        foreach (var key in keys)
        {
            contents[key] = this.CompareContent(key, kind, localJson[key], remoteJson[key]);
        }

        return contents;
    }

    private NodeContentComparison CompareContent(Guid key, PlanItemKind kind, string? localJson, (string? Json, string? Error) remote)
    {
        var what = kind == PlanItemKind.Media ? "media item" : "document";
        if (localJson is null)
        {
            return new NodeContentComparison(null, $"The {what} could not be read here.");
        }

        if (remote.Json is null)
        {
            return new NodeContentComparison(null, remote.Error);
        }

        try
        {
            var comparison = kind == PlanItemKind.Media ? MediaComparer.Compare(localJson, remote.Json) : ContentComparer.Compare(localJson, remote.Json);
            return new NodeContentComparison(comparison, null);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            LogCompareFailed(this.logger, kind, key, exception);
            return new NodeContentComparison(null, $"The {what} could not be compared: " + exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Compared {Kind} tree level {ParentKey} with remote {Alias}: {Rows} rows, {Items} compared ({Different} different, {Failed} failed), {Blocked} types blocked by schema, {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogCompared(ILogger logger, PlanItemKind kind, Guid? parentKey, string alias, int rows, int items, int different, int failed, int blocked, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Compared {Kind} subtree {ParentKey} with remote {Alias}: {Levels} levels, {Nodes} rows, {Requests} remote requests, truncated {Truncated}, {Milliseconds} ms")]
    private static partial void LogSubtreeCompared(ILogger logger, PlanItemKind kind, Guid? parentKey, string alias, int levels, int nodes, int requests, bool truncated, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading {Kind} {Key} from remote {Alias} failed")]
    private static partial void LogItemFailed(ILogger logger, PlanItemKind kind, Guid key, string alias, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparing {Kind} {Key} failed")]
    private static partial void LogCompareFailed(ILogger logger, PlanItemKind kind, Guid key, Exception exception);
}