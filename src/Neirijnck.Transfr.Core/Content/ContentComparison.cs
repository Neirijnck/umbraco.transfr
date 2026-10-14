namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// The outcome of comparing one document on both sides: identical, or the list of what differs.
/// </summary>
public sealed record ContentComparison(IReadOnlyList<ContentDifference> Differences)
{
    public bool IsIdentical => this.Differences.Count == 0;
}