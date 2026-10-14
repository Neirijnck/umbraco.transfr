namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// One child node in a level of a content tree, on one side. SortIndex is its position among its siblings.
/// </summary>
public sealed record TreeNode(Guid Key, string Name, int SortIndex, bool HasChildren);