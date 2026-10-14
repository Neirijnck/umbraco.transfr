namespace Neirijnck.Transfr.Core.References;

/// <summary>
/// One reference from a document's content to a document, media item or member, by key.
/// </summary>
/// <param name="Kind">What it points at.</param>
/// <param name="Key">The key of what it points at.</param>
/// <param name="PropertyAlias">The document property it is in (for a block: the property that holds the block).</param>
/// <param name="Culture">That property value's culture (null = invariant).</param>
/// <param name="Segment">That property value's segment (null = none).</param>
public sealed record ContentReference(ReferenceKind Kind, Guid Key, string PropertyAlias, string? Culture, string? Segment);