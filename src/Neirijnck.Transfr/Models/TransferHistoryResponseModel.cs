namespace Neirijnck.Transfr.Models;

/// <summary>
/// A page of transfers from here to a remote, newest first. NextBefore is passed as before for the next page (null
/// when this is the last one).
/// </summary>
public sealed record TransferHistoryResponseModel(IReadOnlyList<TransferHistoryItemResponseModel> Transfers, int? NextBefore);