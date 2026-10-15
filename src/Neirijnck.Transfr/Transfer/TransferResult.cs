using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// The report of a transfer.
/// </summary>
/// <param name="Status">How it ended.</param>
/// <param name="Rows">Every plan row with what happened to it (empty when refused or busy).</param>
/// <param name="Drift">Why it was refused (empty otherwise).</param>
/// <param name="SortedParents">Parents whose children were put in order on the remote.</param>
/// <param name="Error">For a failed sort or copy history: what went wrong.</param>
/// <param name="RemoteRequests">Requests it cost on the remote, the new plan included.</param>
public sealed record TransferResult(
    TransferStatus Status,
    IReadOnlyList<TransferRow> Rows,
    IReadOnlyList<PlanDrift> Drift,
    int SortedParents,
    string? Error,
    int RemoteRequests);