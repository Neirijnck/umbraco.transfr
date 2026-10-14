namespace Neirijnck.Transfr.Models;

/// <summary>
/// One property value (or a block's value) on both sides. Label is the property's name in this environment's schema,
/// else its alias.
/// </summary>
public sealed record PropertyDiffResponseModel(
    string Alias,
    string Label,
    string? Culture,
    string? Segment,
    string? EditorAlias,
    ValueDiffResponseModel Value);