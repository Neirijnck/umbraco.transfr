namespace Neirijnck.Transfr.Models;

/// <summary>
/// The documents to plan a copy of.
/// </summary>
public sealed record CopyPlanRequestModel(IReadOnlyList<PlanSelectionRequestModel> Selection);