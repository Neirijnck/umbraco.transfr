namespace Neirijnck.Transfr.Local;

/// <summary>
/// A content type's name and its property labels by alias (case-insensitive, compositions included).
/// </summary>
public sealed record ContentTypeNames(string Name, IReadOnlyDictionary<string, string> PropertyLabels);