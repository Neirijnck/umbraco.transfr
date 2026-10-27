using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A remote as the side a copy reads from, through its Management API. Counts every request it sends. Its item
/// responses carry no depth, so its nodes have level 0 (the plan orders them parents first itself).
/// </summary>
internal sealed class RemotePlanSource : IPlanSource
{
    private readonly RemoteDefinition remote;
    private readonly RemoteTreeReader tree;
    private readonly RemoteDocumentReader documents;
    private readonly RemoteMediaReader media;
    private int requests;

    public RemotePlanSource(RemoteDefinition remote, RemoteTreeReader tree, RemoteDocumentReader documents, RemoteMediaReader media)
    {
        this.remote = remote;
        this.tree = tree;
        this.documents = documents;
        this.media = media;
    }

    public int Requests => this.requests;

    public string Place => $"on {this.remote.Name}";

    public async Task<IReadOnlyDictionary<Guid, LocalNode>> DescribeDocumentsAsync(IReadOnlyCollection<Guid> keys, string? culture, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, LocalNode>();
        }

        var (items, sent) = await this.tree.DescribeAsync(this.remote, keys, culture, PlanItemKind.Document, cancellationToken);
        this.requests += sent;
        return items.ToDictionary(pair => pair.Key, pair => new LocalNode(pair.Key, pair.Value.ParentKey, pair.Value.Name, 0));
    }

    public async Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, PlanItemKind kind, CancellationToken cancellationToken)
    {
        var (nodes, sent) = await this.tree.ReadLevelAsync(this.remote, parentKey, culture, cancellationToken, kind);
        this.requests += sent;
        return nodes;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
    {
        var found = await RemoteReads.EachAsync(this.remote, keys, this.documents.ReadAsync, cancellationToken);
        this.requests += keys.Count;
        return found;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> MediaNamesAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var (items, sent) = await this.tree.DescribeAsync(this.remote, keys, null, PlanItemKind.Media, cancellationToken);
        this.requests += sent;
        return items.ToDictionary(pair => pair.Key, pair => pair.Value.Name);
    }

    // A batch per level up: the items, then the folders above them that aren't known yet, until the root.
    public async Task<IReadOnlyDictionary<Guid, LocalMediaNode>> MediaChainsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
    {
        var chains = new Dictionary<Guid, LocalMediaNode>();
        var wanted = keys.Distinct().ToList();
        while (wanted.Count > 0)
        {
            var (items, sent) = await this.tree.DescribeAsync(this.remote, wanted, null, PlanItemKind.Media, cancellationToken);
            this.requests += sent;
            foreach (var (key, item) in items)
            {
                chains[key] = new LocalMediaNode(key, item.ParentKey, item.Name);
            }

            wanted = items.Values.Select(item => item.ParentKey).OfType<Guid>().Where(parent => !chains.ContainsKey(parent)).Distinct().ToList();
        }

        return chains;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
    {
        var found = await RemoteReads.EachAsync(this.remote, keys, this.media.ReadAsync, cancellationToken);
        this.requests += keys.Count;
        return found;
    }
}