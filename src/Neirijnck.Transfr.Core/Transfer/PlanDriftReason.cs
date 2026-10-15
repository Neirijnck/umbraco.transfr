namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// Why a transfer can't go ahead with a row as the dry run showed it.
/// </summary>
public enum PlanDriftReason
{
    /// <summary>The document was saved on either side since the dry run.</summary>
    Saved,

    /// <summary>The outcome is different now (something around it changed: a parent, a link, the schema).</summary>
    OutcomeChanged,

    /// <summary>The plan holds a document the dry run didn't show.</summary>
    NotInDryRun,

    /// <summary>The dry run showed a document the plan no longer holds.</summary>
    NoLongerPlanned,
}