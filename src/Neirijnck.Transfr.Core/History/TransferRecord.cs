namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// One transfer to a remote as the copy history recorded it: the documents it wrote.
/// </summary>
/// <param name="At">When it was recorded.</param>
/// <param name="UserKey">The backoffice user here who ran it.</param>
/// <param name="Documents">The documents written, in the order they were recorded.</param>
public sealed record TransferRecord(DateTimeOffset At, Guid UserKey, IReadOnlyList<CopyHistoryEntry> Documents);