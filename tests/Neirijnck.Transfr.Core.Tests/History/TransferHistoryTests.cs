using System.Globalization;
using Neirijnck.Transfr.Core.History;
using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Tests.History;

/// <summary>
/// Rows shaped like the copy history measured on test site A (four transfers to B, one of them three documents).
/// </summary>
public sealed class TransferHistoryTests
{
    private static readonly Guid Admin = new("1e70f841-c261-413b-abb2-2d68cdb96094");
    private static readonly Guid Editor = new("5f6a7b8c-9d0e-4f1a-8b2c-3d4e5f6a7b8c");
    private static readonly Guid About = new("c50dd09b-25b4-4b3b-b2c1-eb21237a585a");
    private static readonly Guid Features = new("965261de-b2de-4250-9bc0-ddcfa4d57751");
    private static readonly Guid UiTest = new("0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private static readonly Guid TransferTest = new("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");
    private static readonly Guid SortFirst = new("2c3d4e5f-6a7b-4c8d-9e0f-1a2b3c4d5e6f");

    // Newest first, as the store reads them.
    private static readonly CopyHistoryEntry[] Rows =
    [
        Row(9, About, "Update", Admin, "10:52:47.6602278"),
        Row(8, UiTest, "Create", Admin, "10:25:28.2757756"),
        Row(7, Features, "Update", Admin, "10:25:28.2757756"),
        Row(6, About, "Update", Admin, "10:25:28.2757756"),
        Row(5, About, "Update", Admin, "10:04:08.2750271"),
        Row(4, About, "Update", Admin, "10:01:15.3183607"),
        Row(3, SortFirst, "Create", Admin, "10:01:15.3183607"),
        Row(2, TransferTest, "Create", Admin, "10:01:15.3183607"),
    ];

    [Fact]
    public void Group_MakesOneTransferPerMomentNewestFirst_WithItsDocumentsInRecordedOrder()
    {
        var transfers = TransferHistory.Group(Rows);

        Assert.Equal([1, 3, 1, 3], transfers.Select(transfer => transfer.Documents.Count));
        Assert.Equal(At("10:52:47.6602278"), transfers[0].At);
        Assert.Equal([About, Features, UiTest], transfers[1].Documents.Select(document => document.DocumentKey));
        Assert.Equal([TransferTest, SortFirst, About], transfers[3].Documents.Select(document => document.DocumentKey));
        Assert.All(transfers, transfer => Assert.Equal(Admin, transfer.UserKey));
    }

    [Fact]
    public void Group_SplitsTheSameMomentByUser()
    {
        CopyHistoryEntry[] rows = [Row(3, About, "Update", Editor, "10:00:00"), Row(2, Features, "Update", Admin, "10:00:00")];

        var transfers = TransferHistory.Group(rows);

        Assert.Equal([Editor, Admin], transfers.Select(transfer => transfer.UserKey));
    }

    [Fact]
    public void Group_SplitsTheSameMomentByDirection()
    {
        CopyHistoryEntry[] rows = [Row(3, About, "Update", Admin, "10:00:00", CopyDirection.FromRemote), Row(2, Features, "Update", Admin, "10:00:00")];

        var transfers = TransferHistory.Group(rows);

        Assert.Equal([CopyDirection.FromRemote, CopyDirection.ToRemote], transfers.Select(transfer => transfer.Direction));
    }

    [Fact]
    public void Group_OfNoRowsIsEmpty()
    {
        Assert.Empty(TransferHistory.Group([]));
    }

    [Theory]
    [InlineData(2, false, false)] // A third transfer has started: the second is whole.
    [InlineData(3, false, true)] // The third may continue in rows not read yet.
    [InlineData(4, false, true)]
    [InlineData(3, true, false)] // No more rows: whatever was read is whole.
    [InlineData(5, true, false)]
    public void NeedsMoreRows_UntilTheLastTransferOfThePageIsKnownToBeWhole(int take, bool noMoreRows, bool expected)
    {
        var transfers = TransferHistory.Group(Rows[..5]); // Rows 9 to 5: the transfers of 10:52, 10:25 and 10:04.

        Assert.Equal(3, transfers.Count);
        Assert.Equal(expected, TransferHistory.NeedsMoreRows(transfers, take, noMoreRows));
    }

    [Fact]
    public void Cut_ReturnsThePageAndTheIdToReadTheNextOneBefore()
    {
        var (page, nextBefore) = TransferHistory.Cut(TransferHistory.Group(Rows), 2);

        Assert.Equal(2, page.Count);
        Assert.Equal(6, nextBefore); // The oldest row of the page's last transfer.
        var next = TransferHistory.Group(Rows.Where(row => row.Id < nextBefore).ToList());
        Assert.Equal(At("10:04:08.2750271"), next[0].At);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Cut_HasNoNextPageWhenEverythingFits(int take)
    {
        var (page, nextBefore) = TransferHistory.Cut(TransferHistory.Group(Rows), take);

        Assert.Equal(4, page.Count);
        Assert.Null(nextBefore);
    }

    [Fact]
    public void Cut_RefusesAnEmptyPage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TransferHistory.Cut(TransferHistory.Group(Rows), 0));
    }

    private static CopyHistoryEntry Row(int id, Guid document, string outcome, Guid user, string time, CopyDirection direction = CopyDirection.ToRemote)
        => new(id, document, outcome, user, At(time), PlanItemKind.Document, direction);

    private static DateTimeOffset At(string time)
        => DateTimeOffset.Parse($"2026-10-15T{time}+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}