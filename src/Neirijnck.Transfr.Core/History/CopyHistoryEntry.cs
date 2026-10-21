using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// One recorded copy of a document or media item to a remote.
/// </summary>
/// <param name="Id">The row's id: rows are only added, so a higher id is a later copy.</param>
/// <param name="DocumentKey">The document or media item, the same key on both sides.</param>
/// <param name="Outcome">Create or Update.</param>
/// <param name="UserKey">The backoffice user here who ran the transfer.</param>
/// <param name="CopiedAt">When the transfer was recorded: one moment for all of its rows.</param>
/// <param name="Kind">A document or a media item.</param>
public sealed record CopyHistoryEntry(int Id, Guid DocumentKey, string Outcome, Guid UserKey, DateTimeOffset CopiedAt, PlanItemKind Kind);