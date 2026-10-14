namespace Neirijnck.Transfr.Local;

/// <summary>
/// Names from this environment's schema, by key: content types (document and element types) and templates.
/// </summary>
public sealed record SchemaNames(IReadOnlyDictionary<Guid, ContentTypeNames> Types, IReadOnlyDictionary<Guid, string> Templates);