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
    private static readonly Guid TestPage = new("e245cdaa-4176-4551-a6c4-ff3ead958d31");
    private static readonly Guid VariantPageType = new("4219aaf3-5468-437f-a14f-a2980f650693");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ParseTreePage_ReadsTheRootLevel()
    {
        var (total, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-root.json"), 0, "en-US");

        Assert.Equal(1, total);
        Assert.Equal(new TreeNode(Home, "Home", 0, true, TestPage, "icon-document"), Assert.Single(nodes));
    }

    [Fact]
    public void ParseTreePage_ReadsChildrenInOrderWithTheirSortIndex()
    {
        var (total, nodes) = ManagementApiTreeParser.ParseTreePage(Fixture("tree-children.json"), 0, "en-US");

        Assert.Equal(5, total);
        Assert.Equal(["About", "News", "Draft Page", "Features", "Variant Page"], nodes.Select(n => n.Name));
        Assert.Equal([0, 1, 2, 3, 4], nodes.Select(n => n.SortIndex));
        Assert.Equal(new TreeNode(News, "News", 1, true, TestPage, "icon-document"), nodes[1]);
        Assert.Equal((VariantPageType, "icon-globe"), (nodes[4].DocumentTypeKey, nodes[4].DocumentTypeIcon));
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
    public void ParseItemLocations_SaysWhichDocumentsArePublishedInSomeCulture()
    {
        // Variant states as item/document returns them in 17.7.0; a trashed item is left out as by ParseItemParents.
        const string json = """
            [{"isTrashed":false,"parent":null,"variants":[{"state":"Draft","culture":"en-US"},{"state":"PublishedPendingChanges","culture":"nl-BE"}],"id":"1371df8b-89da-4984-8cfb-501a72d1ad9d"},
             {"isTrashed":false,"parent":{"id":"1371df8b-89da-4984-8cfb-501a72d1ad9d"},"variants":[{"state":"Draft","culture":null}],"id":"924c1efa-9b3d-4575-9106-166e6ed39ad1"},
             {"isTrashed":true,"parent":{"id":"1371df8b-89da-4984-8cfb-501a72d1ad9d"},"variants":[{"state":"Trashed","culture":null}],"id":"a008f488-fde5-4f76-9e99-dd37e65c4800"}]
            """;

        var locations = ManagementApiTreeParser.ParseItemLocations(json);

        Assert.Equal(new ItemLocation(null, true), locations[Home]);
        Assert.Equal(new ItemLocation(Home, false), locations[About]);
        Assert.False(locations.ContainsKey(News));
    }

    [Fact]
    public void ParseItemParents_ReadsMediaItemsToo()
    {
        // item/media as measured on an Umbraco 17.7.0 test site: Blue and Red asked for with a third, unknown key,
        // which the API left out.
        const string json = """
            [{"isTrashed":false,"parent":{"id":"a3f1f94d-30a7-433b-8fc9-3ba35f61f72b"},"hasChildren":false,"mediaType":{"id":"cc07b313-0843-4aa8-bbda-871c8da728c8","icon":"icon-picture","collection":null},"variants":[{"name":"Blue","culture":null}],"id":"a28038ed-b11a-41f6-a990-bed021cac2fb","flags":[]},
             {"isTrashed":false,"parent":{"id":"1b3682a5-7914-4a6c-9f2e-ace6937d4169"},"hasChildren":false,"mediaType":{"id":"cc07b313-0843-4aa8-bbda-871c8da728c8","icon":"icon-picture","collection":null},"variants":[{"name":"Red","culture":null}],"id":"d7055f49-d608-40a3-94d4-d4b5cbd0facb","flags":[]}]
            """;

        var parents = ManagementApiTreeParser.ParseItemParents(json);

        Assert.Equal(new Guid("a3f1f94d-30a7-433b-8fc9-3ba35f61f72b"), parents[new Guid("a28038ed-b11a-41f6-a990-bed021cac2fb")]);
        Assert.Equal(new Guid("1b3682a5-7914-4a6c-9f2e-ace6937d4169"), parents[new Guid("d7055f49-d608-40a3-94d4-d4b5cbd0facb")]);
        Assert.Equal(2, parents.Count);
    }

    [Fact]
    public void ParseMediaItems_KeepsTrashedMediaItems_WithoutTheirRecycleBinParent()
    {
        // item/media as measured on an Umbraco 17.7.0 test site; the second item as a trashed one is listed (isTrashed, the
        // recycle bin as its parent).
        const string json = """
            [{"isTrashed":false,"parent":{"id":"a3f1f94d-30a7-433b-8fc9-3ba35f61f72b"},"hasChildren":false,"mediaType":{"id":"cc07b313-0843-4aa8-bbda-871c8da728c8","icon":"icon-picture","collection":null},"variants":[{"name":"Blue","culture":null}],"id":"a28038ed-b11a-41f6-a990-bed021cac2fb","flags":[]},
             {"isTrashed":true,"parent":{"id":"bf7c7cbc-952f-4518-97a2-69e9c7b33842"},"hasChildren":false,"mediaType":{"id":"cc07b313-0843-4aa8-bbda-871c8da728c8","icon":"icon-picture","collection":null},"variants":[{"name":"Red","culture":null}],"id":"d7055f49-d608-40a3-94d4-d4b5cbd0facb","flags":[]},
             {"isTrashed":false,"parent":null,"hasChildren":true,"mediaType":{"id":"f38bd2d7-65d0-48e6-95dc-87ce06ec2d3d","icon":"icon-folder","collection":null},"variants":[{"name":"Images","culture":null}],"id":"a3f1f94d-30a7-433b-8fc9-3ba35f61f72b","flags":[]}]
            """;

        var items = ManagementApiTreeParser.ParseMediaItems(json);

        Assert.Equal((new Guid("a3f1f94d-30a7-433b-8fc9-3ba35f61f72b"), false), items[new Guid("a28038ed-b11a-41f6-a990-bed021cac2fb")]);
        Assert.Equal(((Guid?)null, true), items[new Guid("d7055f49-d608-40a3-94d4-d4b5cbd0facb")]);
        Assert.Equal(((Guid?)null, false), items[new Guid("a3f1f94d-30a7-433b-8fc9-3ba35f61f72b")]);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void ParseTreePage_GivesANodeWithoutANameAPlaceholder()
    {
        const string json = """{"total":1,"items":[{"id":"924c1efa-9b3d-4575-9106-166e6ed39ad1","hasChildren":false,"variants":[{"name":"","culture":null}]}]}""";

        var (_, nodes) = ManagementApiTreeParser.ParseTreePage(json, 0, null);

        Assert.Equal(ManagementApiTreeParser.UnnamedNode, Assert.Single(nodes).Name);
    }
}