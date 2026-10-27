namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// The latest recorded copy of a document or media item between this environment and a remote, either way.
/// </summary>
/// <param name="Direction">Which way it was copied.</param>
/// <param name="TargetUpdate">The target's last save right after the copy.</param>
/// <param name="SourceUpdate">
/// The source's last save when it was copied; null for copies recorded before both sides were kept.
/// </param>
public sealed record RecordedCopy(CopyDirection Direction, DateTimeOffset TargetUpdate, DateTimeOffset? SourceUpdate);