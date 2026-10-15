using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// A transfer only carries out what its dry run showed: it plans again and compares document by document. Any drift
/// refuses the whole transfer, so nothing is written on the strength of an outdated overview. Media rows aren't
/// compared: nothing writes media yet.
/// </summary>
public static class PlanCheck
{
    /// <returns>Every difference, in the order of the plan, then rows the plan no longer holds; empty when it matches.</returns>
    public static IReadOnlyList<PlanDrift> Compare(IReadOnlyList<PlanRow> plan, IReadOnlyList<DryRunRow> dryRun)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(dryRun);

        var shown = new Dictionary<Guid, DryRunRow>();
        foreach (var row in dryRun)
        {
            shown.TryAdd(row.Key, row);
        }

        var drift = new List<PlanDrift>();
        var documents = plan.Where(row => row.Kind == PlanItemKind.Document).ToList();
        foreach (var row in documents)
        {
            if (!shown.TryGetValue(row.Key, out var before))
            {
                drift.Add(new PlanDrift(row.Key, row.Name, PlanDriftReason.NotInDryRun, null, row.Outcome));
            }
            else if (before.Outcome != row.Outcome)
            {
                drift.Add(new PlanDrift(row.Key, row.Name, PlanDriftReason.OutcomeChanged, before.Outcome, row.Outcome));
            }
            else if (!string.Equals(before.Fingerprint, row.Fingerprint, StringComparison.Ordinal))
            {
                drift.Add(new PlanDrift(row.Key, row.Name, PlanDriftReason.Saved, before.Outcome, row.Outcome));
            }
        }

        var planned = documents.Select(row => row.Key).ToHashSet();
        drift.AddRange(shown.Values
            .Where(row => !planned.Contains(row.Key))
            .Select(row => new PlanDrift(row.Key, null, PlanDriftReason.NoLongerPlanned, row.Outcome, null)));
        return drift;
    }
}