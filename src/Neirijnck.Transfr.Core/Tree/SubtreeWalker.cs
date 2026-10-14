namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// Compares a whole subtree, one level at a time, breadth first: the children of the start, then their children, and so
/// on. It goes into a node when either side says it has children, and into each node once, so a node that shows up
/// under two parents (moved) has its children compared a single time.
/// </summary>
public static class SubtreeWalker
{
    /// <param name="start">The parent whose descendants are compared (null = the whole tree).</param>
    /// <param name="compareLevel">Compares the children of one parent.</param>
    /// <param name="rowsOf">The rows of a compared level.</param>
    /// <param name="maxNodes">
    /// Stops before the next level once this many rows were compared, so a huge site can't keep a request busy for
    /// minutes. The levels compared so far are returned, flagged as truncated.
    /// </param>
    /// <param name="cancellationToken">Stops the walk, for instance when the request is aborted.</param>
    public static async Task<(IReadOnlyList<(Guid? ParentKey, TLevel Level)> Levels, bool Truncated)> WalkAsync<TLevel>(
        Guid? start,
        Func<Guid?, CancellationToken, Task<TLevel>> compareLevel,
        Func<TLevel, IReadOnlyList<TreeCompareRow>> rowsOf,
        int maxNodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compareLevel);
        ArgumentNullException.ThrowIfNull(rowsOf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        var levels = new List<(Guid? ParentKey, TLevel Level)>();
        var queue = new Queue<Guid?>();
        var entered = new HashSet<Guid>();
        queue.Enqueue(start);
        if (start is { } startKey)
        {
            entered.Add(startKey);
        }

        var nodes = 0;
        while (queue.Count > 0)
        {
            if (nodes >= maxNodes)
            {
                return (levels, true);
            }

            var parentKey = queue.Dequeue();
            var level = await compareLevel(parentKey, cancellationToken);
            levels.Add((parentKey, level));

            var rows = rowsOf(level);
            nodes += rows.Count;
            foreach (var row in rows)
            {
                var hasChildren = row.Local?.HasChildren == true || row.Remote?.HasChildren == true;
                if (hasChildren && entered.Add(row.Key))
                {
                    queue.Enqueue(row.Key);
                }
            }
        }

        return (levels, false);
    }
}