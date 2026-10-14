namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// Compares the children of one parent on both sides, by key. Comparing level by level keeps a large site fast:
/// only expanded levels are read.
/// </summary>
public static class TreeLevelComparer
{
    /// <summary>
    /// The keys that are under this parent on one side only. The caller looks them up on the other side (one batch
    /// per side) to tell "elsewhere" from "missing", then passes the result to <see cref="Compare"/>.
    /// </summary>
    public static (IReadOnlyList<Guid> LocalOnly, IReadOnlyList<Guid> RemoteOnly) KeysToLocate(
        IReadOnlyList<TreeNode> local,
        IReadOnlyList<TreeNode> remote)
    {
        var localKeys = KeySet(local, nameof(local));
        var remoteKeys = KeySet(remote, nameof(remote));
        return (
            local.Where(node => !remoteKeys.Contains(node.Key)).Select(node => node.Key).ToList(),
            remote.Where(node => !localKeys.Contains(node.Key)).Select(node => node.Key).ToList());
    }

    /// <summary>
    /// Compares one level.
    /// </summary>
    /// <param name="local">The children of the parent locally.</param>
    /// <param name="remote">The children of the parent on the remote.</param>
    /// <param name="remoteParentsOfLocalOnly">
    /// For local-only keys that exist on the remote under another parent: that parent (null = the root). Keys not in
    /// the dictionary don't exist on the remote.
    /// </param>
    /// <param name="localParentsOfRemoteOnly">The same, the other way round.</param>
    /// <returns>
    /// One row per key, in local order; a node only on the remote follows the node that precedes it on the remote.
    /// </returns>
    public static IReadOnlyList<TreeCompareRow> Compare(
        IReadOnlyList<TreeNode> local,
        IReadOnlyList<TreeNode> remote,
        IReadOnlyDictionary<Guid, Guid?> remoteParentsOfLocalOnly,
        IReadOnlyDictionary<Guid, Guid?> localParentsOfRemoteOnly)
    {
        ArgumentNullException.ThrowIfNull(remoteParentsOfLocalOnly);
        ArgumentNullException.ThrowIfNull(localParentsOfRemoteOnly);
        KeySet(local, nameof(local));
        KeySet(remote, nameof(remote));

        var remoteByKey = remote.ToDictionary(node => node.Key);
        var localByKey = local.ToDictionary(node => node.Key);

        var rows = local
            .OrderBy(node => node.SortIndex)
            .Select(node => remoteByKey.TryGetValue(node.Key, out var counterpart)
                ? new TreeCompareRow(node.Key, node, counterpart, TreeCompareStatus.InBoth)
                : remoteParentsOfLocalOnly.TryGetValue(node.Key, out var otherParent)
                    ? new TreeCompareRow(node.Key, node, null, TreeCompareStatus.ElsewhereOnRemote, otherParent)
                    : new TreeCompareRow(node.Key, node, null, TreeCompareStatus.OnlyLocal))
            .ToList();

        // Place each remote-only node after the node that precedes it on the remote.
        var insertAfter = -1;
        foreach (var node in remote.OrderBy(node => node.SortIndex))
        {
            if (localByKey.ContainsKey(node.Key))
            {
                insertAfter = rows.FindIndex(row => row.Key == node.Key);
                continue;
            }

            var row = localParentsOfRemoteOnly.TryGetValue(node.Key, out var otherParent)
                ? new TreeCompareRow(node.Key, null, node, TreeCompareStatus.ElsewhereLocally, otherParent)
                : new TreeCompareRow(node.Key, null, node, TreeCompareStatus.OnlyRemote);
            rows.Insert(++insertAfter, row);
        }

        return LinkSameNames(rows);
    }

    // A node that exists on one side only, with a namesake (other key) that exists on the other side only: two
    // separately created look-alikes, so link both. A node found elsewhere is the same node, not a look-alike.
    private static List<TreeCompareRow> LinkSameNames(List<TreeCompareRow> rows)
    {
        var localOnly = rows.Where(row => row.Status == TreeCompareStatus.OnlyLocal).ToList();
        var remoteOnly = rows.Where(row => row.Status == TreeCompareStatus.OnlyRemote).ToList();

        return rows
            .Select(row =>
            {
                var namesake = row.Status switch
                {
                    TreeCompareStatus.OnlyLocal => remoteOnly.FirstOrDefault(other => SameName(other.Remote!.Name, row.Local!.Name)),
                    TreeCompareStatus.OnlyRemote => localOnly.FirstOrDefault(other => SameName(other.Local!.Name, row.Remote!.Name)),
                    _ => null,
                };
                return namesake is null ? row : row with { SameNameKey = namesake.Key };
            })
            .ToList();
    }

    private static bool SameName(string left, string right)
        => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static HashSet<Guid> KeySet(IReadOnlyList<TreeNode> nodes, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(nodes, parameterName);

        var keys = new HashSet<Guid>();
        foreach (var node in nodes)
        {
            if (!keys.Add(node.Key))
            {
                throw new ArgumentException($"Key {node.Key} appears more than once.", parameterName);
            }
        }

        return keys;
    }
}