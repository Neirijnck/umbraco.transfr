using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// One compared tree level, with the content of every node found on both sides and the number of requests it cost on
/// the remote.
/// </summary>
/// <param name="Rows">The rows, as <see cref="TreeLevelComparer"/> classified them.</param>
/// <param name="Contents">By key, for nodes that exist on both sides (also when under another parent).</param>
/// <param name="RemoteRequests">The requests the level cost on the remote.</param>
public sealed record TreeLevelComparison(
    IReadOnlyList<TreeCompareRow> Rows,
    IReadOnlyDictionary<Guid, NodeContentComparison> Contents,
    int RemoteRequests);