namespace Neirijnck.Transfr.Models;

/// <summary>
/// The dry run of a copy to a remote: media rows first, then the documents, parents before their children.
/// SeparateInstall lists evidence that the remote was installed separately (empty when there is none).
/// </summary>
public sealed record CopyPlanResponseModel(
    IReadOnlyList<PlanRowResponseModel> Rows,
    IReadOnlyList<SeparateInstallResponseModel> SeparateInstall,
    int RemoteRequests);