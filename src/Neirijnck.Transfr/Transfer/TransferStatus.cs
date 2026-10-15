namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// How a transfer ended.
/// </summary>
public enum TransferStatus
{
    /// <summary>Every row was carried out.</summary>
    Completed,

    /// <summary>A write (or the sort) failed; rows after it were not written. What was written stays written.</summary>
    Failed,

    /// <summary>The plan no longer matches the dry run: nothing was written.</summary>
    Refused,

    /// <summary>Another transfer to the same remote is running: nothing was written.</summary>
    Busy,
}