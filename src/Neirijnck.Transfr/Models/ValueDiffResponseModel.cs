namespace Neirijnck.Transfr.Models;

/// <summary>
/// A value on both sides as text (null = no value, or a block editor whose content is in Blocks).
/// </summary>
public sealed record ValueDiffResponseModel(bool Differs, string? Local, string? Remote, IReadOnlyList<BlockDiffResponseModel> Blocks);