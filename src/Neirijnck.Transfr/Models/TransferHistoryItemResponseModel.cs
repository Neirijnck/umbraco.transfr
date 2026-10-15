namespace Neirijnck.Transfr.Models;

/// <summary>
/// One transfer: when, by whom here (null when that user no longer exists), and the documents it wrote.
/// </summary>
public sealed record TransferHistoryItemResponseModel(DateTimeOffset At, string? By, IReadOnlyList<TransferHistoryDocumentResponseModel> Documents);