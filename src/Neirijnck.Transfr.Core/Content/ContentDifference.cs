namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// One difference between the two sides of a document.
/// </summary>
/// <param name="Kind">What differs.</param>
/// <param name="Alias">For a value: the property alias.</param>
/// <param name="Culture">For a name, state or value: the culture (null = invariant).</param>
/// <param name="Segment">For a name, state or value: the segment (null = none).</param>
public sealed record ContentDifference(ContentDifferenceKind Kind, string? Alias = null, string? Culture = null, string? Segment = null);