namespace Neirijnck.Transfr.Models;

/// <summary>
/// A compared subtree: every compared level, breadth first. Truncated: the node limit was reached and deeper levels
/// were not compared. SeparateInstall: evidence from any level that the remote was installed separately.
/// </summary>
public sealed record SubtreeResponseModel(
    IReadOnlyList<SubtreeLevelResponseModel> Levels,
    IReadOnlyList<SeparateInstallResponseModel> SeparateInstall,
    int RemoteRequests,
    bool Truncated);