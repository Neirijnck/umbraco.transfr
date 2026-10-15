namespace Neirijnck.Transfr.Models;

/// <summary>
/// The report of a transfer. Status is Completed, Failed (a row or the sort failed; what was written stays written),
/// Refused (the plan no longer matches the dry run: Drift says why, nothing was written) or Busy (another transfer to
/// this remote is running). Error explains a failed sort or copy history, or why it is busy.
/// </summary>
public sealed record TransferResponseModel(
    string Status,
    IReadOnlyList<TransferRowResponseModel> Rows,
    IReadOnlyList<PlanDriftResponseModel> Drift,
    int SortedParents,
    string? Error,
    int RemoteRequests);