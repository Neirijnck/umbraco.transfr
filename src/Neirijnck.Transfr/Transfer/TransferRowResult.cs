namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// What a transfer did with one row.
/// </summary>
public enum TransferRowResult
{
    /// <summary>Created or updated on the remote.</summary>
    Written,

    /// <summary>The same on both sides: nothing to write (it may still have been placed among its siblings).</summary>
    Unchanged,

    /// <summary>A conflict not chosen to overwrite.</summary>
    Skipped,

    /// <summary>Blocked in the plan.</summary>
    Blocked,

    /// <summary>Writing it failed; the transfer stopped here.</summary>
    Failed,

    /// <summary>Not written because an earlier row failed.</summary>
    NotRun,

    /// <summary>A media item: copying media comes later.</summary>
    MediaLater,
}