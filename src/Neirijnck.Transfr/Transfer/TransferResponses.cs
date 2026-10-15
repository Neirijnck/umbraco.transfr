using Neirijnck.Transfr.Models;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// A <see cref="TransferResult"/> as the API answers it.
/// </summary>
internal static class TransferResponses
{
    public static TransferResponseModel From(TransferResult result)
        => new(
            result.Status.ToString(),
            result.Rows
                .Select(row => new TransferRowResponseModel(
                    row.Row.Kind.ToString(),
                    row.Row.Key,
                    row.Row.Name,
                    row.Row.Outcome.ToString(),
                    row.Result.ToString(),
                    row.Row.PublishCultures,
                    row.Error,
                    row.TargetState))
                .ToList(),
            result.Drift
                .Select(drift => new PlanDriftResponseModel(drift.Key, drift.Name, drift.Reason.ToString(), drift.Was?.ToString(), drift.Now?.ToString()))
                .ToList(),
            result.SortedParents,
            result.Error,
            result.RemoteRequests);
}