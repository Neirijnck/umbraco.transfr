using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Compares one tree level: reads it on both sides, looks up the keys found on one side only (one batch per side),
/// classifies every node with <see cref="TreeLevelComparer"/>, and compares the content of every node that exists on
/// both sides with <see cref="ContentComparer"/>.
/// </summary>
public sealed partial class TreeCompareService
{
    // Remote document requests in flight at once: one level of a large site stays quick without flooding the remote.
    private const int MaxParallelDocumentRequests = 4;

    private readonly LocalTreeReader localTree;
    private readonly RemoteTreeReader remoteTree;
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly ILanguageService languageService;
    private readonly ILogger<TreeCompareService> logger;

    public TreeCompareService(
        LocalTreeReader localTree,
        RemoteTreeReader remoteTree,
        LocalDocumentReader localDocuments,
        RemoteDocumentReader remoteDocuments,
        ILanguageService languageService,
        ILogger<TreeCompareService> logger)
    {
        this.localTree = localTree;
        this.remoteTree = remoteTree;
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.languageService = languageService;
        this.logger = logger;
    }

    public async Task<TreeLevelComparison> CompareLevelAsync(RemoteDefinition remote, Guid? parentKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Names are shown in this environment's default language; the remote's names in the same culture.
        var culture = await this.languageService.GetDefaultIsoCodeAsync();

        var local = this.localTree.ReadLevel(parentKey, culture);
        var (remoteNodes, readRequests) = await this.remoteTree.ReadLevelAsync(remote, parentKey, culture, cancellationToken);

        var (localOnly, remoteOnly) = TreeLevelComparer.KeysToLocate(local, remoteNodes);
        var (remoteParents, locateRequests) = await this.remoteTree.LocateAsync(remote, localOnly.ToList(), cancellationToken);
        var localParents = this.localTree.Locate(remoteOnly.ToList());

        var rows = TreeLevelComparer.Compare(local, remoteNodes, remoteParents, localParents);

        // A node under another parent on one side still exists on both: its content is compared too.
        var bothSides = rows.Where(row => row.Status != TreeCompareStatus.OnlyLocal && row.Status != TreeCompareStatus.OnlyRemote)
            .Select(row => row.Key)
            .ToList();
        var contents = await this.CompareContentsAsync(remote, bothSides, cancellationToken);
        var requests = readRequests + locateRequests + bothSides.Count;
        var different = contents.Values.Count(content => content.Comparison is { IsIdentical: false });
        var failed = contents.Values.Count(content => content.Error is not null);

        LogCompared(this.logger, parentKey, remote.Alias, rows.Count, contents.Count, different, failed, requests, stopwatch.ElapsedMilliseconds);
        return new TreeLevelComparison(rows, contents, requests);
    }

    private async Task<IReadOnlyDictionary<Guid, NodeContentComparison>> CompareContentsAsync(
        RemoteDefinition remote,
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        // Local reads one at a time: Umbraco's ambient scope is not thread-safe.
        var localJson = new Dictionary<Guid, string?>();
        foreach (var key in keys)
        {
            localJson[key] = await this.localDocuments.ReadAsync(key);
        }

        var remoteJson = new ConcurrentDictionary<Guid, (string? Json, string? Error)>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDocumentRequests, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (key, token) =>
        {
            try
            {
                remoteJson[key] = (await this.remoteDocuments.ReadAsync(remote, key, token), null);
            }
            catch (HttpRequestException exception)
            {
                // One unreadable document fails its own row, not the level. A refused sign-in still fails the level.
                LogDocumentFailed(this.logger, key, remote.Alias, exception);
                remoteJson[key] = (null, exception.Message);
            }
        });

        var contents = new Dictionary<Guid, NodeContentComparison>();
        foreach (var key in keys)
        {
            contents[key] = this.CompareContent(key, localJson[key], remoteJson[key]);
        }

        return contents;
    }

    private NodeContentComparison CompareContent(Guid key, string? localJson, (string? Json, string? Error) remote)
    {
        if (localJson is null)
        {
            return new NodeContentComparison(null, "The document could not be read here.");
        }

        if (remote.Json is null)
        {
            return new NodeContentComparison(null, remote.Error);
        }

        try
        {
            return new NodeContentComparison(ContentComparer.Compare(localJson, remote.Json), null);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            LogCompareFailed(this.logger, key, exception);
            return new NodeContentComparison(null, "The document could not be compared: " + exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Compared tree level {ParentKey} with remote {Alias}: {Rows} rows, {Documents} documents compared ({Different} different, {Failed} failed), {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogCompared(ILogger logger, Guid? parentKey, string alias, int rows, int documents, int different, int failed, int requests, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading document {Key} from remote {Alias} failed")]
    private static partial void LogDocumentFailed(ILogger logger, Guid key, string alias, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparing the content of document {Key} failed")]
    private static partial void LogCompareFailed(ILogger logger, Guid key, Exception exception);
}