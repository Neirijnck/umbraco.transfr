namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// The baseline for conflict detection: how the latest copy, in either direction, left the side a new copy would write
/// to. A copy changes only its target, so right after it both sides are as recorded; a later copy the other way finds
/// its target (the earlier copy's source) unchanged as long as it still carries the date recorded for that side.
/// </summary>
public static class CopyBaseline
{
    /// <summary>
    /// The last save of the planned copy's target as the latest copy left it; null when that side wasn't recorded.
    /// </summary>
    public static DateTimeOffset? TargetUpdate(RecordedCopy latest, CopyDirection planned)
    {
        ArgumentNullException.ThrowIfNull(latest);
        return latest.Direction == planned ? latest.TargetUpdate : latest.SourceUpdate;
    }
}