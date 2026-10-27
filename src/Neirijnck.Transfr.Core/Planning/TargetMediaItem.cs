namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// The target's copy of a media item or folder on the source.
/// </summary>
/// <param name="ParentKey">Its parent folder on the target (null = root; meaningless when trashed).</param>
/// <param name="IsTrashed">Whether it is in the target's recycle bin.</param>
/// <param name="Json">
/// Its <c>GET media/{id}</c> JSON, for a media item the copied documents reference (null for a folder above one, and for
/// a trashed item).
/// </param>
public sealed record TargetMediaItem(Guid? ParentKey, bool IsTrashed, string? Json);