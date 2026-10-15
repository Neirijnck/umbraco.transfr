using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// One row of a transfer report.
/// </summary>
/// <param name="Row">The plan row it carried out.</param>
/// <param name="Result">What happened.</param>
/// <param name="Error">For a failure: what the remote answered.</param>
/// <param name="TargetState">For a write or a failure: the document's publish state on the remote afterwards ("absent" when it isn't there).</param>
public sealed record TransferRow(PlanRow Row, TransferRowResult Result, string? Error, string? TargetState);