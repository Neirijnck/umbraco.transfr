using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// One compared tree level, with the number of requests it cost on the remote.
/// </summary>
public sealed record TreeLevelComparison(IReadOnlyList<TreeCompareRow> Rows, int RemoteRequests);