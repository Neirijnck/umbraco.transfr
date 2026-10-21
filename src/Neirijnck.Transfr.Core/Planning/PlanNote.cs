namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// One reason or remark on a plan row.
/// </summary>
/// <param name="Kind">What it says.</param>
/// <param name="Key">The item it is about: the referenced document, media item or member, the parent, or the namesake.</param>
/// <param name="PropertyAlias">For a reference: the property it is in.</param>
/// <param name="Culture">For a publish remark or a reference: the culture (null = invariant).</param>
public sealed record PlanNote(PlanNoteKind Kind, Guid? Key = null, string? PropertyAlias = null, string? Culture = null)
{
    public bool Blocks => this.Kind is PlanNoteKind.SchemaMismatch or PlanNoteKind.ParentMissingOnTarget or PlanNoteKind.ParentBlocked
        or PlanNoteKind.MissingReference or PlanNoteKind.MemberReference or PlanNoteKind.SameNameOtherKey
        or PlanNoteKind.TrashedOnTarget or PlanNoteKind.DocumentTypeDiffers or PlanNoteKind.MediaTypeDiffers;
}