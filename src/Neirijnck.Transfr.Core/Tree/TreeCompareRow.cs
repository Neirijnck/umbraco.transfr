namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// One row of a compared tree level.
/// </summary>
/// <param name="Key">The node's key, the identity on both sides.</param>
/// <param name="Local">The node under this parent locally, if there.</param>
/// <param name="Remote">The node under this parent on the remote, if there.</param>
/// <param name="Status">Where the node exists.</param>
/// <param name="OtherParentKey">For a node found elsewhere: its parent on the other side (null = the root).</param>
/// <param name="SameNameKey">
/// For a node on one side only: the key of a node with the same name under this parent on the other side. Two
/// separately created nodes that look alike; copying would duplicate them, so this blocks a copy later.
/// </param>
public sealed record TreeCompareRow(
    Guid Key,
    TreeNode? Local,
    TreeNode? Remote,
    TreeCompareStatus Status,
    Guid? OtherParentKey = null,
    Guid? SameNameKey = null);