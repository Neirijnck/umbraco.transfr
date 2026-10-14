namespace Neirijnck.Transfr.Models;

/// <summary>
/// One row of a compared tree level. Status is one of InBoth, OnlyLocal, OnlyRemote, ElsewhereOnRemote,
/// ElsewhereLocally. Content is set for a node found on both sides.
/// </summary>
public sealed record TreeRowResponseModel(
    Guid Key,
    string Status,
    TreeSideResponseModel? Local,
    TreeSideResponseModel? Remote,
    Guid? OtherParentKey,
    Guid? SameNameKey,
    TreeRowContentResponseModel? Content);