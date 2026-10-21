namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// What a schema item is, as the Management API serves it: <c>document-type/{id}</c> (document and element types),
/// <c>data-type/{id}</c> or <c>media-type/{id}</c>.
/// </summary>
public enum SchemaItemKind
{
    DocumentType,
    DataType,
    MediaType,
}