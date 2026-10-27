namespace Neirijnck.Transfr.Models;

/// <summary>
/// The documents and media items to plan a copy of, and which way: ToRemote (or left out) copies from here to the
/// remote, FromRemote from the remote to here (the selection then names the remote's items).
/// </summary>
public sealed record CopyPlanRequestModel(IReadOnlyList<PlanSelectionRequestModel> Selection, string? Direction = null);