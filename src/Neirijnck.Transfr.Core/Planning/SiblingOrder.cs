namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Where copied nodes go among their siblings on the target. A new node goes right after its nearest preceding sibling
/// here that is on the target, or first when it has none. A node that is already there moves the same way, but only
/// when it is out of order: outside the longest run of siblings that are in the same order on both sides (as the tree's
/// "Order differs"). Nodes that weren't copied never move, so the run is chosen to keep as many of them as it can, then
/// as many copied ones; nodes in order stay put around a node that wasn't copied.
/// </summary>
public static class SiblingOrder
{
    /// <param name="targetOrder">The parent's children on the target, in order (without the nodes still to create).</param>
    /// <param name="localOrder">The same parent's children here, in order.</param>
    /// <param name="copied">The copied nodes under that parent, new ones included.</param>
    /// <returns>The children on the target in their new order, the new nodes included.</returns>
    public static IReadOnlyList<Guid> Place(IReadOnlyList<Guid> targetOrder, IReadOnlyList<Guid> localOrder, IReadOnlySet<Guid> copied)
    {
        ArgumentNullException.ThrowIfNull(targetOrder);
        ArgumentNullException.ThrowIfNull(localOrder);
        ArgumentNullException.ThrowIfNull(copied);

        var targetIndex = targetOrder.Select((key, index) => (key, index)).ToDictionary(pair => pair.key, pair => pair.index);
        var shared = localOrder.Where(targetIndex.ContainsKey).ToList();
        var inOrder = HeaviestRun(shared.Select(key => targetIndex[key]).ToList(), shared.Select(key => !copied.Contains(key)).ToList())
            .Select(index => shared[index])
            .ToHashSet();

        // A copied node that isn't a child here (it shouldn't be) stays where it is, or isn't added when it's new.
        var placeable = localOrder.Where(key => copied.Contains(key) && !inOrder.Contains(key)).ToHashSet();
        var result = targetOrder.Where(key => !placeable.Contains(key)).ToList();

        // In local order, so a run of copied nodes follows one another.
        for (var index = 0; index < localOrder.Count; index++)
        {
            var key = localOrder[index];
            if (!placeable.Contains(key))
            {
                continue;
            }

            var position = 0;
            for (var before = index - 1; before >= 0; before--)
            {
                var found = result.IndexOf(localOrder[before]);
                if (found >= 0)
                {
                    position = found + 1;
                    break;
                }
            }

            result.Insert(position, key);
        }

        return result;
    }

    // The indexes of the increasing run (by target position) that keeps the most fixed nodes, then the most others:
    // O(n²) over one parent's children, which a plan bounds.
    private static HashSet<int> HeaviestRun(List<int> positions, List<bool> isFixed)
    {
        // A fixed node outweighs any number of movable ones.
        var weights = isFixed.Select(fixedNode => fixedNode ? positions.Count + 1L : 1L).ToList();
        var best = new long[positions.Count];
        var previous = new int[positions.Count];
        for (var i = 0; i < positions.Count; i++)
        {
            best[i] = weights[i];
            previous[i] = -1;
            for (var j = 0; j < i; j++)
            {
                if (positions[j] < positions[i] && best[j] + weights[i] > best[i])
                {
                    best[i] = best[j] + weights[i];
                    previous[i] = j;
                }
            }
        }

        var run = new HashSet<int>();
        var end = positions.Count == 0 ? -1 : Enumerable.Range(0, positions.Count).MaxBy(index => best[index]);
        for (var i = end; i >= 0; i = previous[i])
        {
            run.Add(i);
        }

        return run;
    }

    /// <summary>The node just before this one in an order (null when it is first, or not in the order).</summary>
    public static Guid? PredecessorOf(IReadOnlyList<Guid> order, Guid key)
    {
        ArgumentNullException.ThrowIfNull(order);
        var index = order.ToList().IndexOf(key);
        return index > 0 ? order[index - 1] : null;
    }
}