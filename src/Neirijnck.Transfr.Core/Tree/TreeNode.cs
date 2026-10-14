namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// One child node in a level of a content tree, on one side. SortIndex is its position among its siblings;
/// DocumentTypeKey and DocumentTypeIcon its document type on that side, when known.
/// </summary>
public sealed record TreeNode(
    Guid Key,
    string Name,
    int SortIndex,
    bool HasChildren,
    Guid? DocumentTypeKey = null,
    string? DocumentTypeIcon = null);