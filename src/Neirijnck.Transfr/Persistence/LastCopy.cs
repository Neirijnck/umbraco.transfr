using Neirijnck.Transfr.Core.History;

namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// The latest copy of a document or media item between here and a remote, either way, as the copy history recorded it.
/// </summary>
/// <param name="TargetUpdate">
/// The planned copy's target's last save as that copy left it: the baseline for conflict detection
/// (<see cref="CopyBaseline"/>); null when that side wasn't recorded.
/// </param>
/// <param name="CopiedAt">When the copy was written.</param>
/// <param name="UserKey">The backoffice user here who ran it.</param>
/// <param name="Direction">Which way it was copied.</param>
public sealed record LastCopy(DateTimeOffset? TargetUpdate, DateTimeOffset CopiedAt, Guid UserKey, CopyDirection Direction);