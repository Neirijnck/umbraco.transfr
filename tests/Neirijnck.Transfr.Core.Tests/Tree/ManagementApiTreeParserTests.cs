using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Core.Tests.Tree;

/// <summary>
/// Runs against responses captured from an Umbraco 17.7.0 test site (Fixtures/*.json).
/// </summary>
public sealed class ManagementApiTreeParserTests
{
    private static readonly Guid Home = new("1371df8b-89da-4984-8cfb-501a72d1ad9d");
    private static readonly Guid About = new("924c1efa-9b3d-4575-9106-166e6ed39ad1");
    private static readonly Guid News = new("a008f488-fde5-4f76-9e99-dd37e65c4800");
    private static readonly Guid VariantPage = new("444a58ce-840f-46d2-8b8a-2fcdfc0916e4");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ParseTreePage_ReadsTheRootLevel()
    {
        var (total, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-root.json"), 0, "en-US");

        Assert.Equal(1, total);
        Assert.Equal(new TreeNode(Home, "Home", 0, true), Assert.Single(nodes));
    }

    [Fact]
    public void ParseTreePage_ReadsChildrenInOrderWithTheirSortIndex()
    {
        var (total, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-children.json"), 0, "en-US");

        Assert.Equal(5, total);
        Assert.Equal(["About", "News", "Draft Page", "Features", "Variant Page"], nodes.Select(n => n.Name));
        Assert.Equal([0, 1, 2, 3, 4], nodes.Select(n => n.SortIndex));
        Assert.Equal(new TreeNode(News, "News", 1, true), nodes[1]);
    }

    [Fact]
    public void ParseTreePage_OffsetsTheSortIndexBySkip()
    {
        var (_, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-children.json"), 100, "en-US");

        Assert.Equal(100, nodes[0].SortIndex);
    }

    [Theory]
    [InlineData("en-US", "Variant Page")]
    [InlineData("nl-BE", "Variante Pagina")]
    [InlineData("fr-FR", "Variant Page")]
    [InlineData(null, "Variant Page")]
    public void ParseTreePage_NamesAVariantNodeInThePreferredCultureElseTheFirst(string? culture, string expected)
    {
        var (_, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-children.json"), 0, culture);

        Assert.Equal(expected, nodes.Single(n => n.Key == VariantPage).Name);
    }

    [Fact]
    public void ParseItemParents_MapsKeysToParents_AndLeavesOutUnknownKeys()
    {
        // The fixture asked for four keys, one of them unknown: the API answered with three.
        var parents = ManagementApiTreeParser.ParseItemParents(Fixture("item-document.json"));

        Assert.Equal(3, parents.Count);
        Assert.Null(parents[Home]);
        Assert.Equal(Home, parents[About]);
        Assert.Equal(Home, parents[News]);
    }

    [Fact]
    public void ParseItemParents_TreatsATrashedDocumentAsMissing()
    {
        const string json = """
            [{"isTrashed":true,"parent":{"id":"1371df8b-89da-4984-8cfb-501a72d1ad9d"},"id":"924c1efa-9b3d-4575-9106-166e6ed39ad1"},
             {"isTrashed":false,"parent":null,"id":"a008f488-fde5-4f76-9e99-dd37e65c4800"}]
            """;

        var parents = ManagementApiTreeParser.ParseItemParents(json);

        Assert.Equal([News], parents.Keys);
    }

    [Fact]
    public void ParseTreePage_GivesANodeWithoutANameAPlaceholder()
    {
        const string json = """{"total":1,"items":[{"id":"924c1efa-9b3d-4575-9106-166e6ed39ad1","hasChildren":false,"variants":[{"name":"","culture":null}]}]}""";

        var (_, nodes) = ManagementApiTreeParser.ParseTreePage(json, 0, null);

        Assert.Equal(ManagementApiTreeParser.UnnamedNode, Assert.Single(nodes).Name);
    }
}