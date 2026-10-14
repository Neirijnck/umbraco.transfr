namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// A child of a parent on the target.
/// </summary>
/// <param name="ParentKey">The parent (null = root).</param>
/// <param name="Key">The child's key.</param>
/// <param name="Name">The child's name.</param>
public sealed record TargetSibling(Guid? ParentKey, Guid Key, string Name);