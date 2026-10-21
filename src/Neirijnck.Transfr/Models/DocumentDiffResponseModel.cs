namespace Neirijnck.Transfr.Models;

/// <summary>
/// One document or media item on both sides, field by field, for the diff view. DocumentTypeName is the local document
/// type's (or media type's) name (null when this environment doesn't have it). Files: for a media item, its files on
/// both sides, to show them; null for a document.
/// </summary>
public sealed record DocumentDiffResponseModel(
    bool IsIdentical,
    string? DocumentTypeName,
    IReadOnlyList<FieldDiffResponseModel> Fields,
    IReadOnlyList<PropertyDiffResponseModel> Properties,
    IReadOnlyList<MediaFileDiffResponseModel>? Files = null);