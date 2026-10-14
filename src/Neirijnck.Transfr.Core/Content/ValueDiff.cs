namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// A value on both sides. Local and Remote are the value as text: a string as it is, rich text as its markup, anything
/// else as indented JSON without what the comparison ignores; null when the side has no value, and for a block editor,
/// whose content is in <see cref="Blocks"/>.
/// </summary>
public sealed record ValueDiff(bool Differs, string? Local, string? Remote, IReadOnlyList<BlockDiff> Blocks);