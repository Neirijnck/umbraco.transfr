using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Tests.Planning;

public sealed class ParentsFirstTests
{
    private static readonly Guid Home = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid News = new("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Article = new("33333333-3333-4333-8333-333333333333");
    private static readonly Guid About = new("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Elsewhere = new("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Loop = new("66666666-6666-4666-8666-666666666666");

    private static List<Guid> Order(params (Guid Key, Guid? Parent)[] items)
        => ParentsFirst.Order(items, item => item.Key, item => item.Parent).Select(item => item.Key).ToList();

    [Fact]
    public void Order_KeepsAListThatIsAlreadyParentsFirst()
    {
        Assert.Equal([Home, About, News, Article], Order((Home, null), (About, Home), (News, Home), (Article, News)));
    }

    [Fact]
    public void Order_PutsAChildRightAfterItsParent_WhenThatParentComesLater()
    {
        Assert.Equal([About, Home, Article], Order((Article, Home), (About, Elsewhere), (Home, null)));
    }

    [Fact]
    public void Order_WaitsDownAWholeChain()
    {
        Assert.Equal([Home, News, Article], Order((Article, News), (News, Home), (Home, null)));
    }

    [Fact]
    public void Order_IgnoresParentsOutsideTheList()
    {
        Assert.Equal([Article, About], Order((Article, News), (About, Home)));
    }

    [Fact]
    public void Order_KeepsItemsWhoseParentsLoop()
    {
        Assert.Equal([Home, Loop, News], Order((Home, null), (Loop, News), (News, Loop)));
    }
}