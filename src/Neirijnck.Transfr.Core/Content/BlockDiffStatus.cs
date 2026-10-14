namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// How one block compares: the same, other content or settings, only in another place on the page, or on one side only.
/// </summary>
public enum BlockDiffStatus
{
    Same,
    Changed,
    Moved,
    OnlyLocal,
    OnlyRemote,
}