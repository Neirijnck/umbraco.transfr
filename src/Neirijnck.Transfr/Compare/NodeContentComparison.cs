using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// The content of one node compared on both sides, or why that could not be done.
/// </summary>
public sealed record NodeContentComparison(ContentComparison? Comparison, string? Error);