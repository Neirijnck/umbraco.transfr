namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document picked for a copy, with or without everything below it, or only some of its fields (Fields set; then
/// IncludeDescendants must be false). A document also picked whole is copied whole. Kind is Document (or left out) or
/// Media: a media item, or a folder with or without everything below it (no fields).
/// </summary>
public sealed record PlanSelectionRequestModel(Guid Key, bool IncludeDescendants, IReadOnlyList<FieldChoiceRequestModel>? Fields = null, string? Kind = null);