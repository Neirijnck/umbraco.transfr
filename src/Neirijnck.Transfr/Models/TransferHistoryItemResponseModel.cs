namespace Neirijnck.Transfr.Models;

/// <summary>
/// One transfer: when, by whom here (null when that user no longer exists), the documents and media items it wrote, and
/// which way (ToRemote or FromRemote).
/// </summary>
public sealed record TransferHistoryItemResponseModel(DateTimeOffset At, string? By, IReadOnlyList<TransferHistoryDocumentResponseModel> Documents, string Direction);