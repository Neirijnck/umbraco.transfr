namespace Neirijnck.Transfr.Local;

/// <summary>
/// A document here: where it is and what it is called.
/// </summary>
/// <param name="Key">Its key.</param>
/// <param name="ParentKey">Its parent (null = the root).</param>
/// <param name="Name">Its name, in the culture asked for when it has one.</param>
/// <param name="Level">Its depth: 1 at the root.</param>
public sealed record LocalNode(Guid Key, Guid? ParentKey, string Name, int Level);