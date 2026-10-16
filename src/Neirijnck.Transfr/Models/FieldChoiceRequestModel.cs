namespace Neirijnck.Transfr.Models;

/// <summary>
/// One field picked for a field-level copy: Kind is Name (the node's name in a culture) or Value (a property value;
/// Alias required). Culture and Segment are null for invariant fields.
/// </summary>
public sealed record FieldChoiceRequestModel(string Kind, string? Alias, string? Culture, string? Segment);