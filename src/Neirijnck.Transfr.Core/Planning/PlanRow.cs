using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// One document or media item in a copy plan.
/// </summary>
/// <param name="Kind">Document, or media planned along.</param>
/// <param name="Key">Its key, the same on the target after the copy.</param>
/// <param name="ParentKey">Its parent here (null = root).</param>
/// <param name="Name">Its name here.</param>
/// <param name="DocumentTypeKey">For a document: its document type here.</param>
/// <param name="Outcome">What the copy would do.</param>
/// <param name="Notes">Why, and what to know, in the order they were found.</param>
/// <param name="PublishCultures">
/// For a document that is written: the cultures to publish after writing (null = the invariant content); empty = save only.
/// </param>
/// <param name="Differences">For a document on both sides: what differs.</param>
/// <param name="SchemaProblems">For a schema mismatch: the problems.</param>
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
    IReadOnlyList<SchemaProblem> SchemaProblems);