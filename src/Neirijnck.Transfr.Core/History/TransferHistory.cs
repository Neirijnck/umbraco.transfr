namespace Neirijnck.Transfr.Core.History;

/// <summary>
/// Turns the copy history of one remote into transfers, a page at a time. A transfer records all of its rows in one
/// moment, and only one transfer runs per remote at a time, so among a remote's rows (newest first) a transfer is a run
/// of rows with the same moment and user.
/// </summary>
public static class TransferHistory
{
    /// <summary>Groups a remote's rows, newest first, into transfers, newest first.</summary>
    public static IReadOnlyList<TransferRecord> Group(IReadOnlyList<CopyHistoryEntry> rowsNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(rowsNewestFirst);

        var transfers = new List<TransferRecord>();
        var run = new List<CopyHistoryEntry>();
        foreach (var row in rowsNewestFirst)
        {
            if (run.Count > 0 && (row.CopiedAt != run[0].CopiedAt || row.UserKey != run[0].UserKey))
            {
                transfers.Add(ToTransfer(run));
                run = [];
            }

            run.Add(row);
        }

        if (run.Count > 0)
        {
            transfers.Add(ToTransfer(run));
        }

        return transfers;
    }

    /// <summary>
    /// Whether more rows must be read before a page of <paramref name="take"/> transfers can be cut: the last transfer
    /// of a page is only known to be whole once a later row starts another one, or there are no more rows.
    /// </summary>
    public static bool NeedsMoreRows(IReadOnlyList<TransferRecord> transfers, int take, bool noMoreRows)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        return !noMoreRows && transfers.Count <= take;
    }

    /// <summary>
    /// The first <paramref name="take"/> transfers, and the id to read the next page before (null when this is the
    /// last page). Only valid once <see cref="NeedsMoreRows"/> is false.
    /// </summary>
    public static (IReadOnlyList<TransferRecord> Page, int? NextBefore) Cut(IReadOnlyList<TransferRecord> transfers, int take)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        var page = transfers.Take(take).ToList();
        int? nextBefore = transfers.Count > take ? page[^1].Documents.Min(document => document.Id) : null;
        return (page, nextBefore);
    }

    // Rows were read newest first; a transfer lists its documents in the order it recorded them.
    private static TransferRecord ToTransfer(List<CopyHistoryEntry> run)
        => new(run[0].CopiedAt, run[0].UserKey, run.OrderBy(row => row.Id).ToList());
}