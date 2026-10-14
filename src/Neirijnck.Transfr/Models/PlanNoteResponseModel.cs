namespace Neirijnck.Transfr.Models;

/// <summary>
/// Why a plan row has its outcome, or what to know about it. Kind names the reason (see the Core PlanNoteKind); Blocks
/// says it blocks the row. Key and Name are the item it is about (Name null when not known here), PropertyAlias and
/// PropertyName the property a reference is in, Culture the culture it is about. CanAddToSelection: the document exists
/// here, so adding it to the selection lifts this block.
/// </summary>
public sealed record PlanNoteResponseModel(
    string Kind,
    bool Blocks,
    Guid? Key,
    string? Name,
    string? PropertyAlias,
    string? PropertyName,
    string? Culture,
    bool CanAddToSelection);