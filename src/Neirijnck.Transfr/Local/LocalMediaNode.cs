namespace Neirijnck.Transfr.Local;

/// <summary>
/// A media item or folder here: where it is and what it is called.
/// </summary>
/// <param name="Key">Its key.</param>
/// <param name="ParentKey">Its parent folder (null = the root).</param>
/// <param name="Name">Its name.</param>
public sealed record LocalMediaNode(Guid Key, Guid? ParentKey, string Name);