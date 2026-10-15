using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Tests.Planning;

public sealed class SiblingOrderTests
{
    private static readonly Guid A = new("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid B = new("bbbbbbbb-0000-4000-8000-000000000002");
    private static readonly Guid C = new("cccccccc-0000-4000-8000-000000000003");
    private static readonly Guid D = new("dddddddd-0000-4000-8000-000000000004");
    private static readonly Guid N = new("eeeeeeee-0000-4000-8000-000000000005");
    private static readonly Guid M = new("ffffffff-0000-4000-8000-000000000006");
    private static readonly Guid X = new("99999999-0000-4000-8000-000000000007");

    [Fact]
    public void Place_PutsANewNodeRightAfterItsPredecessorHere()
    {
        Assert.Equal([A, N, B, C], SiblingOrder.Place([A, B, C], [A, N, B, C], new HashSet<Guid> { N }));
    }

    [Fact]
    public void Place_PutsANewNodeWithoutAPredecessorFirst()
    {
        Assert.Equal([N, A, B], SiblingOrder.Place([A, B], [N, A, B], new HashSet<Guid> { N }));
    }

    [Fact]
    public void Place_SkipsPredecessorsThatAreNotOnTheTarget()
    {
        // D isn't on the target, so N goes after the nearest one that is: A.
        Assert.Equal([A, N, X, B], SiblingOrder.Place([A, X, B], [A, D, N, B], new HashSet<Guid> { N }));
    }

    [Fact]
    public void Place_KeepsARunOfNewNodesTogether_InTheirOrderHere()
    {
        Assert.Equal([A, N, M, B], SiblingOrder.Place([A, B], [A, N, M, B], new HashSet<Guid> { N, M }));
    }

    [Fact]
    public void Place_MovesACopiedNodeThatIsOutOfOrder()
    {
        // B and C are in order on both sides; A is first here and last there.
        Assert.Equal([A, B, C], SiblingOrder.Place([B, C, A], [A, B, C], new HashSet<Guid> { A }));
    }

    [Fact]
    public void Place_LeavesNodesInOrderAlone_AroundOneThatWasNotCopied()
    {
        // Test site B's Home: About (Ab) sits last there and wasn't copied (a skipped conflict). News, Draft Page and
        // Variant Page (N, D, V) are in order around Features (F) and stay put; only the new X goes after V.
        var n = Guid.NewGuid();
        var d = Guid.NewGuid();
        var f = Guid.NewGuid();
        var v = Guid.NewGuid();
        var onlyThere = Guid.NewGuid();
        var ab = Guid.NewGuid();

        var result = SiblingOrder.Place([n, d, f, v, onlyThere, ab], [ab, n, d, f, v, X], new HashSet<Guid> { n, d, v, X });

        Assert.Equal([n, d, f, v, X, onlyThere, ab], result);
    }

    [Fact]
    public void Place_MovesTheCopiedNodesAroundOneThatWasNot_WhenAllAreOutOfOrder()
    {
        // Reversed on the target. B can't move, so A and C take their places around it: the order here.
        Assert.Equal([A, B, C], SiblingOrder.Place([C, B, A], [A, B, C], new HashSet<Guid> { A, C }));
    }

    [Fact]
    public void Place_LeavesNodesThatWereNotCopiedWhereTheyAre_EvenOutOfOrder()
    {
        Assert.Equal([A, C, B], SiblingOrder.Place([A, C, B], [A, B, C], new HashSet<Guid> { A }));
    }

    [Fact]
    public void Place_LeavesTheOrderAloneWhenItAlreadyMatches()
    {
        IReadOnlyList<Guid> target = [A, B, C];

        Assert.Equal(target, SiblingOrder.Place(target, [A, B, C], new HashSet<Guid> { A, B, C }));
    }

    [Fact]
    public void Place_LeavesACopiedNodeThatIsNotAChildHereWhereItIs()
    {
        Assert.Equal([A, X, B], SiblingOrder.Place([A, X, B], [A, B], new HashSet<Guid> { X }));
    }

    [Fact]
    public void PredecessorOf_IsTheNodeBefore_OrNullWhenFirstOrAbsent()
    {
        Assert.Equal(A, SiblingOrder.PredecessorOf([A, B], B));
        Assert.Null(SiblingOrder.PredecessorOf([A, B], A));
        Assert.Null(SiblingOrder.PredecessorOf([A, B], C));
    }
}