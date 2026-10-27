using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A remote as the side a copy writes to, read through its Management API. Counts every request it sends.
/// </summary>
internal sealed partial class RemotePlanTarget : IPlanTarget
{
    private readonly RemoteDefinition remote;
    private readonly RemoteTreeReader tree;
    private readonly RemoteDocumentReader documents;
    private readonly RemoteMediaReader media;
    private readonly RemoteAuditReader audit;
    private readonly ILogger logger;
    private int requests;

    public RemotePlanTarget(
        RemoteDefinition remote,
        RemoteTreeReader tree,
        RemoteDocumentReader documents,
        RemoteMediaReader media,
        RemoteAuditReader audit,
        ILogger logger)
    {
        this.remote = remote;
        this.tree = tree;
        this.documents = documents;
        this.media = media;
        this.audit = audit;
        this.logger = logger;
    }

    public int Requests => this.requests;

    public Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => this.ReadEachAsync(keys, this.documents.ReadAsync, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ItemLocation>> LocateDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
    {
        var (locations, sent) = await this.tree.LocateDocumentsAsync(this.remote, keys, cancellationToken);
        Interlocked.Add(ref this.requests, sent);
        return locations;
    }

    public async Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken)
    {
        var (nodes, sent) = await this.tree.ReadLevelAsync(this.remote, parentKey, culture, cancellationToken);
        Interlocked.Add(ref this.requests, sent);
        return nodes;
    }

    public async Task<IReadOnlyDictionary<Guid, (Guid? ParentKey, bool IsTrashed)>> LocateMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
    {
        var (items, sent) = await this.tree.LocateMediaAsync(this.remote, keys, cancellationToken);
        Interlocked.Add(ref this.requests, sent);
        return items;
    }

    public Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => this.ReadEachAsync(keys, this.media.ReadAsync, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, RemoteChangeInfo>> LastChangesAsync(IReadOnlyList<(PlanItemKind Kind, Guid Key)> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, RemoteChangeInfo>();
        }

        var found = new ConcurrentDictionary<Guid, RemoteChange>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = RemoteReads.MaxParallel, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (item, token) =>
        {
            var (kind, key) = item;
            try
            {
                if (await this.audit.LastChangeAsync(this.remote, kind, key, token) is { } change)
                {
                    found[key] = change;
                }
            }
            catch (Exception exception) when (IsUnreadable(exception, token))
            {
                LogChangeUnread(this.logger, exception, this.remote.Alias, kind, key);
            }
        });

        Interlocked.Add(ref this.requests, keys.Count);
        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        if (!found.IsEmpty)
        {
            try
            {
                (names, var sent) = await this.audit.UserNamesAsync(this.remote, found.Values.Select(change => change.UserKey).ToList(), cancellationToken);
                Interlocked.Add(ref this.requests, sent);
            }
            catch (Exception exception) when (IsUnreadable(exception, cancellationToken))
            {
                LogUserNamesUnread(this.logger, exception, this.remote.Alias);
            }
        }

        return found.ToDictionary(
            pair => pair.Key,
            pair => new RemoteChangeInfo(names.GetValueOrDefault(pair.Value.UserKey), pair.Value.At, pair.Value.LogType));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ReadEachAsync(
        IReadOnlyCollection<Guid> keys,
        Func<RemoteDefinition, Guid, CancellationToken, Task<string>> read,
        CancellationToken cancellationToken)
    {
        var found = await RemoteReads.EachAsync(this.remote, keys, read, cancellationToken);
        Interlocked.Add(ref this.requests, keys.Count);
        return found;
    }

    // A refused or failed remote read, an unexpected answer, or a timeout; not the caller cancelling.
    private static bool IsUnreadable(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or JsonException or InvalidOperationException or RemoteAuthenticationException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read who last changed {Kind} {Key} on remote {Alias}")]
    private static partial void LogChangeUnread(ILogger logger, Exception exception, string alias, PlanItemKind kind, Guid key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read user names on remote {Alias}")]
    private static partial void LogUserNamesUnread(ILogger logger, Exception exception, string alias);
}