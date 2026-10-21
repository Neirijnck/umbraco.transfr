namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document or media item a transfer wrote: Outcome is Create or Update; Name is its name here now (null when it is no
/// longer here); Kind is Document or Media.
/// </summary>
public sealed record TransferHistoryDocumentResponseModel(Guid Key, string? Name, string Outcome, string Kind);