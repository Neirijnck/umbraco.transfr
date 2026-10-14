namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// What a copy would do with one document or media item.
/// </summary>
public enum PlanOutcome
{
    /// <summary>Not on the target, and nothing blocks it: created with the same key.</summary>
    Create,

    /// <summary>On the target and different, and the target hasn't changed since our last copy.</summary>
    Update,

    /// <summary>On the target and identical.</summary>
    Unchanged,

    /// <summary>On the target and different, with no copy of ours to go by or changed since: skipped unless chosen.</summary>
    Conflict,

    /// <summary>Can't be copied; the notes say why.</summary>
    Blocked,
}