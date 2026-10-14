using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Core.Tests.Tree;

public sealed class TreeLevelComparerTests
{
    private static readonly Dictionary<Guid, Guid?> Nowhere = [];

    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid E = new("00000000-0000-0000-0000-00000000000e");

    private static TreeNode Node(Guid key, string name, int sortIndex, bool hasChildren = false) => new(key, name, sortIndex, hasChildren);

    [Fact]
    public void Compare_SameChildrenOnBothSides_AreAllInBothInLocalOrder()
    {
        var local = new[] { Node(A, "About", 0), Node(B, "News", 1) };
        var remote = new[] { Node(B, "News", 0), Node(A, "About", 1) };

        var rows = TreeLevelComparer.Compare(local, remote, Nowhere, Nowhere);

        Assert.Equal([A, B], rows.Select(r => r.Key));
        Assert.All(rows, r => Assert.Equal(TreeCompareStatus.InBoth, r.Status));
        Assert.All(rows, r => Assert.NotNull(r.Remote));
    }

    [Fact]
    public void Compare_KeysOnOneSideNotFoundElsewhere_AreOnlyLocalOrOnlyRemote()
    {
        var rows = TreeLevelComparer.Compare([Node(A, "About", 0)], [Node(B, "Contact", 0)], Nowhere, Nowhere);

        Assert.Equal(TreeCompareStatus.OnlyLocal, rows.Single(r => r.Key == A).Status);
        Assert.Equal(TreeCompareStatus.OnlyRemote, rows.Single(r => r.Key == B).Status);
        Assert.All(rows, r => Assert.Null(r.SameNameKey));
    }

    [Fact]
    public void Compare_LocalKeyFoundUnderAnotherRemoteParent_IsElsewhereOnRemote()
    {
        var rows = TreeLevelComparer.Compare([Node(A, "About", 0)], [], new Dictionary<Guid, Guid?> { [A] = D }, Nowhere);

        var row = Assert.Single(rows);
        Assert.Equal(TreeCompareStatus.ElsewhereOnRemote, row.Status);
        Assert.Equal(D, row.OtherParentKey);
    }

    [Fact]
    public void Compare_RemoteKeyFoundAtTheLocalRoot_IsElsewhereLocallyWithNullParent()
    {
        var rows = TreeLevelComparer.Compare([], [Node(A, "About", 0)], Nowhere, new Dictionary<Guid, Guid?> { [A] = null });

        var row = Assert.Single(rows);
        Assert.Equal(TreeCompareStatus.ElsewhereLocally, row.Status);
        Assert.Null(row.OtherParentKey);
    }

    [Fact]
    public void Compare_LookAlikesWithDifferentKeys_AreLinkedBothWays()
    {
        var rows = TreeLevelComparer.Compare([Node(A, "About us", 0)], [Node(B, " about US ", 0)], Nowhere, Nowhere);

        Assert.Equal(B, rows.Single(r => r.Key == A).SameNameKey);
        Assert.Equal(A, rows.Single(r => r.Key == B).SameNameKey);
    }

    [Fact]
    public void Compare_NodeFoundElsewhere_IsNotALookAlike()
    {
        var rows = TreeLevelComparer.Compare(
            [Node(A, "About", 0)],
            [Node(B, "About", 0)],
            new Dictionary<Guid, Guid?> { [A] = D },
            Nowhere);

        Assert.All(rows, r => Assert.Null(r.SameNameKey));
    }

    [Fact]
    public void Compare_LookAlikeOfANodeInBoth_IsNotLinked()
    {
        var rows = TreeLevelComparer.Compare([Node(A, "About", 0)], [Node(A, "About", 0), Node(B, "About", 1)], Nowhere, Nowhere);

        Assert.Null(rows.Single(r => r.Key == B).SameNameKey);
    }

    [Fact]
    public void Compare_RemoteOnlyNodes_FollowTheirRemotePredecessor()
    {
        var local = new[] { Node(A, "A", 0), Node(B, "B", 1), Node(C, "C", 2) };
        var remote = new[] { Node(D, "D", 0), Node(A, "A", 1), Node(E, "E", 2), Node(C, "C", 3) };

        var rows = TreeLevelComparer.Compare(local, remote, Nowhere, Nowhere);

        Assert.Equal([D, A, E, B, C], rows.Select(r => r.Key));
    }

    [Fact]
    public void Compare_UsesSortIndexNotListOrder()
    {
        var rows = TreeLevelComparer.Compare([Node(B, "B", 1), Node(A, "A", 0)], [], Nowhere, Nowhere);

        Assert.Equal([A, B], rows.Select(r => r.Key));
    }

    [Fact]
    public void Compare_EmptyLevels_GiveNoRows()
    {
        Assert.Empty(TreeLevelComparer.Compare([], [], Nowhere, Nowhere));
    }

    [Fact]
    public void Compare_DuplicateKeyOnOneSide_Throws()
    {
        Assert.Throws<ArgumentException>(() => TreeLevelComparer.Compare([Node(A, "A", 0), Node(A, "A", 1)], [], Nowhere, Nowhere));
    }

    [Fact]
    public void KeysToLocate_ReturnsTheKeysOnOneSideOnly()
    {
        var (localOnly, remoteOnly) = TreeLevelComparer.KeysToLocate([Node(A, "A", 0), Node(B, "B", 1)], [Node(B, "B", 0), Node(C, "C", 1)]);

        Assert.Equal([A], localOnly);
        Assert.Equal([C], remoteOnly);
    }

    private static Guid[] OrderDiffers(IReadOnlyList<TreeCompareRow> rows) => rows.Where(r => r.OrderDiffers).Select(r => r.Key).ToArray();

    [Fact]
    public void Compare_TheSameOrderOnBothSides_DiffersNowhere()
    {
        var nodes = new[] { Node(A, "A", 0), Node(B, "B", 1), Node(C, "C", 2) };

        Assert.Empty(OrderDiffers(TreeLevelComparer.Compare(nodes, nodes, Nowhere, Nowhere)));
    }

    [Fact]
    public void Compare_OneNodeMovedToTheEnd_MarksOnlyThatNode()
    {
        var local = new[] { Node(A, "A", 0), Node(B, "B", 1), Node(C, "C", 2), Node(D, "D", 3) };
        var remote = new[] { Node(B, "B", 0), Node(C, "C", 1), Node(D, "D", 2), Node(A, "A", 3) };

        Assert.Equal([A], OrderDiffers(TreeLevelComparer.Compare(local, remote, Nowhere, Nowhere)));
    }

    [Fact]
    public void Compare_TwoNodesSwapped_MarksOneOfThem()
    {
        // Either node could be the one that moved; marking one keeps the count at the fewest moves.
        var rows = TreeLevelComparer.Compare([Node(A, "A", 0), Node(B, "B", 1)], [Node(B, "B", 0), Node(A, "A", 1)], Nowhere, Nowhere);

        Assert.Equal([A], OrderDiffers(rows));
    }

    [Fact]
    public void Compare_AReversedLevel_MarksAllButOne()
    {
        var local = new[] { Node(A, "A", 0), Node(B, "B", 1), Node(C, "C", 2) };
        var remote = new[] { Node(C, "C", 0), Node(B, "B", 1), Node(A, "A", 2) };

        Assert.Equal(2, OrderDiffers(TreeLevelComparer.Compare(local, remote, Nowhere, Nowhere)).Length);
    }

    [Fact]
    public void Compare_NodesOnOneSideOnly_DoNotDisturbTheOrderOfTheRest()
    {
        // D only here, E only on the remote, C elsewhere on the remote: A and B keep their order.
        var local = new[] { Node(D, "D", 0), Node(A, "A", 1), Node(C, "C", 2), Node(B, "B", 3) };
        var remote = new[] { Node(A, "A", 0), Node(E, "E", 1), Node(B, "B", 2) };

        var rows = TreeLevelComparer.Compare(local, remote, new Dictionary<Guid, Guid?> { [C] = null }, Nowhere);

        Assert.Empty(OrderDiffers(rows));
    }

    [Fact]
    public void Compare_OrderDiffersOnlyForNodesInBoth()
    {
        var local = new[] { Node(A, "A", 0), Node(B, "B", 1), Node(C, "C", 2) };
        var remote = new[] { Node(B, "B", 0), Node(A, "A", 1), Node(E, "E", 2) };

        var rows = TreeLevelComparer.Compare(local, remote, Nowhere, Nowhere);

        Assert.All(rows.Where(r => r.Status != TreeCompareStatus.InBoth), r => Assert.False(r.OrderDiffers));
        Assert.Equal([A], OrderDiffers(rows));
    }
}