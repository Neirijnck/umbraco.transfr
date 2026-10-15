namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// Where a document or media item is, and whether it is published.
/// </summary>
/// <param name="ParentKey">Its parent (null = the root).</param>
/// <param name="IsPublished">Published in some culture (always false for a media item).</param>
public sealed record ItemLocation(Guid? ParentKey, bool IsPublished);