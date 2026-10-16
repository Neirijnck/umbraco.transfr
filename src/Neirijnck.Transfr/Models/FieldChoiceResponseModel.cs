namespace Neirijnck.Transfr.Models;

/// <summary>
/// One field a field-level copy carries over: Kind is Name or Value; Label is the property's label here (null for a name).
/// </summary>
public sealed record FieldChoiceResponseModel(string Kind, string? Alias, string? Label, string? Culture, string? Segment);