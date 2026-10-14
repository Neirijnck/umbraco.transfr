namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// A document field on both sides: an id for the document type and template, the text for a name or a state.
/// </summary>
public sealed record FieldDiff(ContentDifferenceKind Kind, string? Culture, string? Segment, string? Local, string? Remote)
{
    public bool Differs => !string.Equals(this.Local, this.Remote, StringComparison.Ordinal);
}