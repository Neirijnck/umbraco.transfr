namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// One block of a block list, block grid or rich text value, matched by key on both sides.
/// </summary>
/// <param name="Key">The block's content key.</param>
/// <param name="Status">How the block compares.</param>
/// <param name="ContentTypeKey">The block's element type.</param>
/// <param name="SettingsTypeKey">The element type of the block's settings, if it has settings.</param>
/// <param name="LocalIndex">Its place on the page locally (null when only on the remote).</param>
/// <param name="RemoteIndex">Its place on the page on the remote (null when only here).</param>
/// <param name="Values">The block's content values.</param>
/// <param name="Settings">The block's settings values, if it has settings.</param>
public sealed record BlockDiff(
    Guid Key,
    BlockDiffStatus Status,
    Guid? ContentTypeKey,
    Guid? SettingsTypeKey,
    int? LocalIndex,
    int? RemoteIndex,
    IReadOnlyList<PropertyDiff> Values,
    IReadOnlyList<PropertyDiff> Settings);