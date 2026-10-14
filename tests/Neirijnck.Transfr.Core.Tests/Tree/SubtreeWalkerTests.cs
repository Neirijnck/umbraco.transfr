using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Core.Tests.Tree;

public sealed class SubtreeWalkerTests
{
    private static readonly Guid Home = new("00000000-0000-0000-0000-0000000000a0");
    private static readonly Guid About = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid News = new("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Article = new("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid Moved = new("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid Child = new("00000000-0000-0000-0000-0000000000a5");

    private static TreeCompareRow InBoth(Guid key, bool hasChildren = false)
        => new(key, new TreeNode(key, "n", 0, hasChildren), new TreeNode(key, "n", 0, hasChildren), TreeCompareStatus.InBoth);

    private static TreeCompareRow OnlyRemote(Guid key, bool hasChildren)
        => new(key, null, new TreeNode(key, "n", 0, hasChildren), TreeCompareStatus.OnlyRemote);

    // Home > (About, News > (Article, Moved > Child)), and Moved shows up under Home too (moved on one side).
    private static readonly Dictionary<Guid, TreeCompareRow[]> Site = new()
    {
        [Guid.Empty] = [InBoth(Home, hasChildren: true)],
        [Home] = [InBoth(About), InBoth(News, hasChildren: true), new(Moved, new TreeNode(Moved, "n", 2, true), null, TreeCompareStatus.ElsewhereOnRemote, News)],
        [News] = [InBoth(Article), new(Moved, null, new TreeNode(Moved, "n", 1, true), TreeCompareStatus.ElsewhereLocally, Home)],
        [Moved] = [OnlyRemote(Child, hasChildren: false)],
    };

    private static async Task<(IReadOnlyList<Guid?> Order, bool Truncated)> Walk(Guid? start, int maxNodes = 1000)
    {
        var (levels, truncated) = await SubtreeWalker.WalkAsync(
            start,
            (parentKey, _) => Task.FromResult(Site.GetValueOrDefault(parentKey ?? Guid.Empty, [])),
            rows => rows,
            maxNodes,
            CancellationToken.None);
        return (levels.Select(level => level.ParentKey).ToList(), truncated);
    }

    [Fact]
    public async Task WalkAsync_ComparesEveryLevelBreadthFirst_FromTheRoot()
    {
        var (order, truncated) = await Walk(null);

        Assert.Equal([null, Home, News, Moved], order);
        Assert.False(truncated);
    }

    [Fact]
    public async Task WalkAsync_StartsAtTheGivenParent()
    {
        var (order, _) = await Walk(News);

        Assert.Equal([News, Moved], order);
    }

    [Fact]
    public async Task WalkAsync_EntersANodeShownUnderTwoParentsOnce()
    {
        var (order, _) = await Walk(null);

        Assert.Single(order, key => key == Moved);
    }

    [Fact]
    public async Task WalkAsync_EntersANodeWhenEitherSideHasChildren_AndNotOtherwise()
    {
        var (order, _) = await Walk(null);

        Assert.DoesNotContain(About, order);
        Assert.DoesNotContain(Child, order);
        Assert.Contains(Moved, order);
    }

    [Fact]
    public async Task WalkAsync_StopsBeforeTheNextLevelOnceTheLimitIsReached()
    {
        // The root level (1 row) and Home (3 rows) make 4: the limit is reached, News is not compared.
        var (order, truncated) = await Walk(null, maxNodes: 4);

        Assert.Equal([null, Home], order);
        Assert.True(truncated);
    }

    [Fact]
    public async Task WalkAsync_IsNotTruncatedWhenTheLastLevelEndsExactlyOnTheLimit()
    {
        // From News: News (2 rows) and Moved (1 row) make 3, and nothing is left.
        var (order, truncated) = await Walk(News, maxNodes: 3);

        Assert.Equal([News, Moved], order);
        Assert.False(truncated);
    }

    [Fact]
    public async Task WalkAsync_RefusesALimitBelowOne()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Walk(null, maxNodes: 0));
    }
}