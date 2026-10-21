namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// One schema item that is missing on a side or differs between the sides.
/// </summary>
/// <param name="Kind">A document (or element) type, a media type, or a data type.</param>
/// <param name="Key">The item's key, the same on both sides when it is the same item.</param>
/// <param name="Name">Its name on the side that has it (the local one when both do).</param>
/// <param name="IsElement">For a document type: whether it is an element type (used by blocks).</param>
/// <param name="Reason">Missing on a side, or different.</param>
/// <param name="Details">For a difference: what differs, in words.</param>
public sealed record SchemaProblem(
    SchemaItemKind Kind,
    Guid Key,
    string? Name,
    bool IsElement,
    SchemaProblemReason Reason,
    IReadOnlyList<string> Details);