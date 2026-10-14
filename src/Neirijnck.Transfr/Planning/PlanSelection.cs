namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A document picked for a copy, with or without everything below it.
/// </summary>
public sealed record PlanSelection(Guid Key, bool IncludeDescendants);