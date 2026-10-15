namespace Neirijnck.Transfr.Models;

/// <summary>
/// One document or media item in a copy plan. Kind is Document or Media; Outcome is Create, Update, Unchanged,
/// Conflict or Blocked. PublishCultures are the cultures published after writing (null = invariant content; empty =
/// saved only). Differences and SchemaProblems are empty when there are none.
/// </summary>
public sealed record PlanRowResponseModel(
    string Kind,
    Guid Key,
    Guid? ParentKey,
    string Name,
    string? DocumentTypeName,
    string Outcome,
    IReadOnlyList<PlanNoteResponseModel> Notes,
    IReadOnlyList<string?> PublishCultures,
    IReadOnlyList<ContentDifferenceResponseModel> Differences,
    IReadOnlyList<SchemaProblemResponseModel> SchemaProblems,
    string? Fingerprint);