namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Orders a selection so that every item comes after its parent when that parent is in the selection too, as the
/// planner needs its sources. Otherwise the order is kept: a list already in that order comes back unchanged, and an
/// item that had to wait follows right after its parent.
/// </summary>
public static class ParentsFirst
{
    public static IReadOnlyList<T> Order<T>(IReadOnlyList<T> items, Func<T, Guid> keyOf, Func<T, Guid?> parentOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keyOf);
        ArgumentNullException.ThrowIfNull(parentOf);

        var inSelection = items.Select(keyOf).ToHashSet();
        var placed = new HashSet<Guid>();
        var waiting = new Dictionary<Guid, List<T>>();
        var ordered = new List<T>(items.Count);

        void Place(T item)
        {
            ordered.Add(item);
            placed.Add(keyOf(item));
            if (waiting.Remove(keyOf(item), out var children))
            {
                foreach (var child in children)
                {
                    Place(child);
                }
            }
        }

        foreach (var item in items)
        {
            if (parentOf(item) is { } parent && inSelection.Contains(parent) && !placed.Contains(parent))
            {
                (waiting.TryGetValue(parent, out var list) ? list : waiting[parent] = []).Add(item);
            }
            else
            {
                Place(item);
            }
        }

        // Only a parent chain that loops (not a tree) leaves items waiting: kept, in their order, rather than lost.
        ordered.AddRange(items.Where(item => !placed.Contains(keyOf(item))));
        return ordered;
    }
}