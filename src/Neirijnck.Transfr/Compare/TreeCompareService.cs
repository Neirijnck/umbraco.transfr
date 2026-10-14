using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Compares one tree level: reads it on both sides, looks up the keys found on one side only (one batch per side),
/// and classifies every node with <see cref="TreeLevelComparer"/>.
/// </summary>
public sealed partial class TreeCompareService
{
    private readonly LocalTreeReader localTree;
    private readonly RemoteTreeReader remoteTree;
    private readonly ILanguageService languageService;
    private readonly ILogger<TreeCompareService> logger;

    public TreeCompareService(LocalTreeReader localTree, RemoteTreeReader remoteTree, ILanguageService languageService, ILogger<TreeCompareService> logger)
    {
        this.localTree = localTree;
        this.remoteTree = remoteTree;
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
        var requests = readRequests + locateRequests;

        LogCompared(this.logger, parentKey, remote.Alias, rows.Count, requests, stopwatch.ElapsedMilliseconds);
        return new TreeLevelComparison(rows, requests);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Compared tree level {ParentKey} with remote {Alias}: {Rows} rows, {Requests} remote requests, {Milliseconds} ms")]
    private static partial void LogCompared(ILogger logger, Guid? parentKey, string alias, int rows, int requests, long milliseconds);
}