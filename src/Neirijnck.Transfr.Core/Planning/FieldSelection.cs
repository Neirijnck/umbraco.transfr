namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// The fields to carry over per node of a selection. A node picked with fields keeps them, merged over every pick of
/// it, unless the selection holds it as a whole node too (picked whole, or below a node picked with everything below
/// it): then the whole node wins.
/// </summary>
public static class FieldSelection
{
    /// <param name="picks">Every pick: a node with its fields (null = the whole node).</param>
    /// <param name="wholeNodes">The nodes the selection holds whole, descendants included.</param>
    /// <returns>The fields of each node copied field by field; a node absent here is copied whole.</returns>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<FieldChoice>> Resolve(
        IEnumerable<(Guid Key, IReadOnlyList<FieldChoice>? Fields)> picks,
        IReadOnlySet<Guid> wholeNodes)
    {
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(wholeNodes);

        var fieldsByKey = new Dictionary<Guid, List<FieldChoice>>();
        foreach (var (key, fields) in picks)
        {
            if (fields is null || wholeNodes.Contains(key))
            {
                continue;
            }

            if (!fieldsByKey.TryGetValue(key, out var merged))
            {
                fieldsByKey[key] = merged = [];
            }

            merged.AddRange(fields.Where(field => !merged.Contains(field)));
        }

        return fieldsByKey.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<FieldChoice>)pair.Value);
    }
}