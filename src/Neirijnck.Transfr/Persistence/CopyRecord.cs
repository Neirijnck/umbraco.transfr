namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// A document just copied, to be recorded in the copy history.
/// </summary>
/// <param name="DocumentKey">The document, the same key on both sides.</param>
/// <param name="Outcome">Create or Update.</param>
/// <param name="TargetUpdateDate">The target's last save right after the copy.</param>
public sealed record CopyRecord(Guid DocumentKey, string Outcome, DateTimeOffset TargetUpdateDate);