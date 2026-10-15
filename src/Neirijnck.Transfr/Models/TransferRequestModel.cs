namespace Neirijnck.Transfr.Models;

/// <summary>
/// A transfer of a dry run: the selection it was made for, its document rows as shown, and the conflicts chosen to
/// overwrite. The server plans again and refuses when anything differs from DryRun.
/// </summary>
public sealed record TransferRequestModel(
    IReadOnlyList<PlanSelectionRequestModel> Selection,
    IReadOnlyList<DryRunRowRequestModel> DryRun,
    IReadOnlyList<Guid> Overwrite);