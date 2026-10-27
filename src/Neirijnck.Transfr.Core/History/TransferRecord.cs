namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// One transfer to or from a remote as the copy history recorded it: the documents and media items it wrote.
/// </summary>
/// <param name="At">When it was recorded.</param>
/// <param name="UserKey">The backoffice user here who ran it.</param>
/// <param name="Documents">The documents written, in the order they were recorded.</param>
/// <param name="Direction">Which way it copied.</param>
public sealed record TransferRecord(DateTimeOffset At, Guid UserKey, IReadOnlyList<CopyHistoryEntry> Documents, CopyDirection Direction);