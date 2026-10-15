namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document a transfer wrote: Outcome is Create or Update; Name is its name here now (null when it is no longer here).
/// </summary>
public sealed record TransferHistoryDocumentResponseModel(Guid Key, string? Name, string Outcome);