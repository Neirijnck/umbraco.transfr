using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// A document or media item just copied, to be recorded in the copy history.
/// </summary>
/// <param name="DocumentKey">The document or media item, the same key on both sides.</param>
/// <param name="Outcome">Create or Update.</param>
/// <param name="TargetUpdateDate">The target's last save right after the copy.</param>
/// <param name="Kind">A document or a media item.</param>
public sealed record CopyRecord(Guid DocumentKey, string Outcome, DateTimeOffset TargetUpdateDate, PlanItemKind Kind);