namespace Neirijnck.Transfr.Models;

/// <summary>
/// One block on both sides. Status is one of Same, Changed, Moved, OnlyLocal, OnlyRemote; the indexes are its place on
/// the page on each side (null where it isn't). ContentTypeName is the element type's name here, if known.
/// </summary>
public sealed record BlockDiffResponseModel(
    Guid Key,
    string Status,
    Guid? ContentTypeKey,
    string? ContentTypeName,
    int? LocalIndex,
    int? RemoteIndex,
    IReadOnlyList<PropertyDiffResponseModel> Values,
    IReadOnlyList<PropertyDiffResponseModel> Settings);