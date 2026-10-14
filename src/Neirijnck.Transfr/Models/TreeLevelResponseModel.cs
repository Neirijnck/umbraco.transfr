namespace Neirijnck.Transfr.Models;

/// <summary>
/// A compared tree level and the number of requests it cost on the remote. SeparateInstall lists evidence that the
/// remote was installed separately (empty when there is none).
/// </summary>
public sealed record TreeLevelResponseModel(
    IReadOnlyList<TreeRowResponseModel> Rows,
    IReadOnlyList<SeparateInstallResponseModel> SeparateInstall,
    int RemoteRequests);