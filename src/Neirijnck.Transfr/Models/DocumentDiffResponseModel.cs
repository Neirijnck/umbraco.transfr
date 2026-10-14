namespace Neirijnck.Transfr.Models;

/// <summary>
/// One document on both sides, field by field, for the diff view. DocumentTypeName is the local document type's name
/// (null when this environment doesn't have it).
/// </summary>
public sealed record DocumentDiffResponseModel(
    bool IsIdentical,
    string? DocumentTypeName,
    IReadOnlyList<FieldDiffResponseModel> Fields,
    IReadOnlyList<PropertyDiffResponseModel> Properties);