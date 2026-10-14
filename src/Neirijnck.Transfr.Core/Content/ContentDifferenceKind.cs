namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// What differs between the two sides of a document.
/// </summary>
public enum ContentDifferenceKind
{
    DocumentType,
    Template,
    Name,
    State,
    Value,
}