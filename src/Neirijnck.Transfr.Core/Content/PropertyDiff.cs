namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// One property value (or a block's value) on both sides.
/// </summary>
public sealed record PropertyDiff(string Alias, string? Culture, string? Segment, string? EditorAlias, ValueDiff Value);