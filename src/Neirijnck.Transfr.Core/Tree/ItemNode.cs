namespace Neirijnck.Transfr.Core.Tree;

/// <summary>A document or media item as an item response describes it.</summary>
/// <param name="ParentKey">Its parent (null = the root).</param>
/// <param name="Name">Its name, in the culture asked for when it has one.</param>
public sealed record ItemNode(Guid? ParentKey, string Name);