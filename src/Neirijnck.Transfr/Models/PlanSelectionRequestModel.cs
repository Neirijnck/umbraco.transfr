namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document picked for a copy, with or without everything below it.
/// </summary>
public sealed record PlanSelectionRequestModel(Guid Key, bool IncludeDescendants);