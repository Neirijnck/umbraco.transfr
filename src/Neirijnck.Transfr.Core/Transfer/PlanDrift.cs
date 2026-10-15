using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// How a document row differs between the dry run and the plan made right before the transfer.
/// </summary>
/// <param name="Key">The document.</param>
/// <param name="Name">Its name now (null when it is no longer in the plan).</param>
/// <param name="Reason">What changed.</param>
/// <param name="Was">The outcome in the dry run (null when it wasn't in it).</param>
/// <param name="Now">The outcome now (null when it is no longer in the plan).</param>
public sealed record PlanDrift(Guid Key, string? Name, PlanDriftReason Reason, PlanOutcome? Was, PlanOutcome? Now);