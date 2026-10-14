namespace Neirijnck.Transfr.Models;

/// <summary>
/// A compared subtree: every compared level, breadth first. Truncated: the node limit was reached and deeper levels
/// were not compared.
/// </summary>
public sealed record SubtreeResponseModel(IReadOnlyList<SubtreeLevelResponseModel> Levels, int RemoteRequests, bool Truncated);