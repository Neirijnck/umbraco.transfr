using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// One document or media item in a copy plan.
/// </summary>
/// <param name="Kind">Document, or media planned along.</param>
/// <param name="Key">Its key, the same on the target after the copy.</param>
/// <param name="ParentKey">Its parent on the source (null = root).</param>
/// <param name="Name">Its name on the source.</param>
/// <param name="DocumentTypeKey">
/// For a document: its document type on the source; for a media item: its media type.
/// </param>
/// <param name="Outcome">What the copy would do.</param>
/// <param name="Notes">Why, and what to know, in the order they were found.</param>
/// <param name="PublishCultures">
/// For a document that is written: the cultures to publish after writing (null = the invariant content); empty = save only.
/// </param>
/// <param name="Differences">For a document or media item on both sides: what differs.</param>
/// <param name="SchemaProblems">For a schema mismatch: the problems.</param>
/// <param name="Fingerprint">
/// What the dry run saw of it on both sides; a transfer refuses when it changed since.
/// </param>
/// <param name="Fields">For a field-level copy: the fields carried over (the differences are theirs only); null = the whole node.</param>
public sealed record PlanRow(
    PlanItemKind Kind,
    Guid Key,
    Guid? ParentKey,
    string Name,
    Guid? DocumentTypeKey,
    PlanOutcome Outcome,
    IReadOnlyList<PlanNote> Notes,
    IReadOnlyList<string?> PublishCultures,
    IReadOnlyList<ContentDifference> Differences,
    IReadOnlyList<SchemaProblem> SchemaProblems,
    string? Fingerprint = null,
    IReadOnlyList<FieldChoice>? Fields = null);