using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Reads a remote's document tree through its Management API. Every method reports how many requests it sent, so
/// the cost of a compare stays visible.
/// </summary>
public sealed class RemoteTreeReader
{
    private const int PageSize = 100;

    // Keys per item/document request: keeps the query string well under common URL length limits.
    private const int LocateBatchSize = 40;

    private readonly RemoteClient client;

    public RemoteTreeReader(RemoteClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// The children of a document (null = the root), by sort order.
    /// </summary>
    public async Task<(IReadOnlyList<TreeNode> Nodes, int Requests)> ReadLevelAsync(
        RemoteDefinition remote,
        Guid? parentKey,
        string? preferredCulture,
        CancellationToken cancellationToken)
    {
        var nodes = new List<TreeNode>();
        var requests = 0;
        int total;
        do
        {
            var path = parentKey is null
                ? $"{RemoteRequestPolicy.ApiBase}/tree/document/root?skip={nodes.Count}&take={PageSize}"
                : $"{RemoteRequestPolicy.ApiBase}/tree/document/children?parentId={parentKey}&skip={nodes.Count}&take={PageSize}";
            var json = await this.client.GetStringAsync(remote, path, cancellationToken);
            requests++;

            var (pageTotal, page) = ManagementApiTreeParser.ParseTreePage(json, nodes.Count, preferredCulture);
            total = pageTotal;
            if (page.Count == 0)
            {
                break;
            }

            nodes.AddRange(page);
        }
        while (nodes.Count < total);

        return (nodes, requests);
    }

    /// <summary>
    /// Where these documents are on the remote: key to parent key (null = the root). Unknown and trashed keys are
    /// left out.
    /// </summary>
    public async Task<(IReadOnlyDictionary<Guid, Guid?> Parents, int Requests)> LocateAsync(
        RemoteDefinition remote,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken)
    {
        var parents = new Dictionary<Guid, Guid?>();
        var requests = 0;
        foreach (var batch in keys.Chunk(LocateBatchSize))
        {
            var query = string.Join("&", batch.Select(key => $"id={key}"));
            var json = await this.client.GetStringAsync(remote, $"{RemoteRequestPolicy.ApiBase}/item/document?{query}", cancellationToken);
            requests++;

            foreach (var (key, parent) in ManagementApiTreeParser.ParseItemParents(json))
            {
                parents[key] = parent;
            }
        }

        return (parents, requests);
    }
}