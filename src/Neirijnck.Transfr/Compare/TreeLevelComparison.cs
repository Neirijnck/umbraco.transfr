using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// One compared tree level, with the content of every node found on both sides and the number of requests it cost on
/// the remote.
/// </summary>
/// <param name="Rows">The rows, as <see cref="TreeLevelComparer"/> classified them.</param>
/// <param name="Contents">By key, for nodes that exist on both sides (also when under another parent).</param>
/// <param name="Schema">The schema checks of the level's document types, by document type key.</param>
/// <param name="DocumentTypeNames">The names of the level's document types (this environment's, else the remote's).</param>
/// <param name="RemoteRequests">The requests the level cost on the remote.</param>
public sealed record TreeLevelComparison(
    IReadOnlyList<TreeCompareRow> Rows,
    IReadOnlyDictionary<Guid, NodeContentComparison> Contents,
    SchemaLevelCheck Schema,
    IReadOnlyDictionary<Guid, string> DocumentTypeNames,
    int RemoteRequests);