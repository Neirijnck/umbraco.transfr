using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// A document row as the dry run showed it, sent back with the transfer.
/// </summary>
/// <param name="Key">The document.</param>
/// <param name="Outcome">What the dry run said would happen.</param>
/// <param name="Fingerprint">What the dry run saw of it on both sides.</param>
public sealed record DryRunRow(Guid Key, PlanOutcome Outcome, string? Fingerprint);