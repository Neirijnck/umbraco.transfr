namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// What a field-level copy can carry over. The publish state is not a field: whether the fields go live follows from
/// both sides (see <see cref="CopyPlanner"/>), so a copy never publishes someone else's draft.
/// </summary>
public enum FieldKind
{
    /// <summary>The node's name in one culture (and segment).</summary>
    Name,

    /// <summary>One property value: an alias in one culture and segment. A block list or grid is one value.</summary>
    Value,
}