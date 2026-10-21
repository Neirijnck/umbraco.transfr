namespace Neirijnck.Transfr.Models;

/// <summary>
/// One row of a transfer report. Result is Written, Unchanged, Skipped (a conflict not overwritten), Blocked, Failed or
/// NotRun (after a failure). TargetState is the publish state on the remote afterwards for a document that wrote or
/// failed, "Saved" for such a media item ("absent" when it isn't there).
/// </summary>
public sealed record TransferRowResponseModel(
    string Kind,
    Guid Key,
    string Name,
    string Outcome,
    string Result,
    IReadOnlyList<string?> PublishCultures,
    string? Error,
    string? TargetState);