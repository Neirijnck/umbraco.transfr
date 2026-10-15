namespace Neirijnck.Transfr.Models;

/// <summary>
/// One row of a transfer report. Result is Written, Unchanged, Skipped (a conflict not overwritten), Blocked, Failed,
/// NotRun (after a failure) or MediaLater. TargetState is the publish state on the remote afterwards for a row that
/// wrote or failed ("absent" when it isn't there).
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