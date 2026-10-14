namespace Neirijnck.Transfr.Models;

/// <summary>
/// A compared tree level and the number of requests it cost on the remote.
/// </summary>
public sealed record TreeLevelResponseModel(IReadOnlyList<TreeRowResponseModel> Rows, int RemoteRequests);