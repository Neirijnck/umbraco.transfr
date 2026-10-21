namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// What differs between the two sides of a document or media item.
/// </summary>
public enum ContentDifferenceKind
{
    DocumentType,
    Template,
    Name,
    State,
    Value,
    MediaType,
}