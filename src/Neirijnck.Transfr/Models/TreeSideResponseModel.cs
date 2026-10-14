namespace Neirijnck.Transfr.Models;

/// <summary>
/// A node on one side of a compared row, with its document type on that side (icon and name, when known).
/// </summary>
public sealed record TreeSideResponseModel(string Name, bool HasChildren, string? DocumentTypeIcon, string? DocumentTypeName);