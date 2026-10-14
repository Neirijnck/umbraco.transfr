namespace Neirijnck.Transfr.Compare;

/// <summary>
/// A compared subtree: every level that was compared, breadth first, with the requests it cost on the remote.
/// </summary>
/// <param name="Levels">Each compared level with its parent (null = the root).</param>
/// <param name="RemoteRequests">The requests the whole subtree cost on the remote.</param>
/// <param name="Truncated">The walk stopped at the node limit; deeper levels were not compared.</param>
public sealed record SubtreeComparison(
    IReadOnlyList<(Guid? ParentKey, TreeLevelComparison Level)> Levels,
    int RemoteRequests,
    bool Truncated);