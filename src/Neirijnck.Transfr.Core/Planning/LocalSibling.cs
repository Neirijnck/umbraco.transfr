namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// A child of a parent here, listed in sort order.
/// </summary>
/// <param name="ParentKey">The parent (null = root).</param>
/// <param name="Key">The child's key.</param>
public sealed record LocalSibling(Guid? ParentKey, Guid Key);