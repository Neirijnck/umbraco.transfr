namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document row as the dry run showed it: its key, outcome and fingerprint, as the plan endpoint answered them.
/// </summary>
public sealed record DryRunRowRequestModel(Guid Key, string Outcome, string? Fingerprint);