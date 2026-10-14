namespace Neirijnck.Transfr.Models;

/// <summary>
/// One difference in a node's content. Kind is one of DocumentType, Template, Name, State, Value; Alias names the
/// property for a Value, Culture and Segment where they apply (null = invariant).
/// </summary>
public sealed record ContentDifferenceResponseModel(string Kind, string? Alias, string? Culture, string? Segment);