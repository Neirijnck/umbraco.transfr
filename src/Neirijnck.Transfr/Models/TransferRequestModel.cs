namespace Neirijnck.Transfr.Models;

/// <summary>
/// A transfer of a dry run: the selection it was made for, its rows as shown, the conflicts chosen to overwrite, and
/// which way (as for the dry run: ToRemote or left out, or FromRemote). The server plans again and refuses when anything
/// differs from DryRun.
/// </summary>
public sealed record TransferRequestModel(
    IReadOnlyList<PlanSelectionRequestModel> Selection,
    IReadOnlyList<DryRunRowRequestModel> DryRun,
    IReadOnlyList<Guid> Overwrite,
    string? Direction = null);