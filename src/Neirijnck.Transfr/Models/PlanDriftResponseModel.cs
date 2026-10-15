namespace Neirijnck.Transfr.Models;

/// <summary>
/// Why a transfer was refused for one document: Reason is Saved (on either side since the dry run), OutcomeChanged,
/// NotInDryRun or NoLongerPlanned; Was and Now are the outcomes in the dry run and now (null where there is none).
/// </summary>
public sealed record PlanDriftResponseModel(Guid Key, string? Name, string Reason, string? Was, string? Now);