namespace Neirijnck.Transfr.Models;

/// <summary>
/// One row of a compared tree level. Status is one of InBoth, OnlyLocal, OnlyRemote, ElsewhereOnRemote,
/// ElsewhereLocally.
/// </summary>
public sealed record TreeRowResponseModel(
    Guid Key,
    string Status,
    TreeSideResponseModel? Local,
    TreeSideResponseModel? Remote,
    Guid? OtherParentKey,
    Guid? SameNameKey);