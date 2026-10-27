namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// A media item or folder on the source (the side copied from).
/// </summary>
/// <param name="Key">Its key.</param>
/// <param name="ParentKey">Its parent folder (null = root).</param>
/// <param name="Name">Its name.</param>
/// <param name="Json">Its <c>GET media/{id}</c> JSON.</param>
public sealed record LocalMediaItem(Guid Key, Guid? ParentKey, string Name, string Json);