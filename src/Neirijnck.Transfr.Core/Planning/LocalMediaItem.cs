namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// A media item or folder here.
/// </summary>
/// <param name="Key">Its key.</param>
/// <param name="ParentKey">Its parent folder (null = root).</param>
/// <param name="Name">Its name.</param>
public sealed record LocalMediaItem(Guid Key, Guid? ParentKey, string Name);