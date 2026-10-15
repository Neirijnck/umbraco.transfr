namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// The latest copy of a document to a remote, as the copy history recorded it.
/// </summary>
/// <param name="TargetUpdate">The target's last save right after the copy: the baseline for conflict detection.</param>
/// <param name="CopiedAt">When the copy was written.</param>
/// <param name="UserKey">The backoffice user here who ran it.</param>
public sealed record LastCopy(DateTimeOffset TargetUpdate, DateTimeOffset CopiedAt, Guid UserKey);