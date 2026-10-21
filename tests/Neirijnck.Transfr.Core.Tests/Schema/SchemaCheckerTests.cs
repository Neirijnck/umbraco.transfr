using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Tests.Schema;

/// <summary>
/// Runs against <c>document-type/{id}</c>, <c>media-type/{id}</c> and <c>data-type/{id}</c> responses captured from an
/// Umbraco 17.7.0 test site (Fixtures/schema/*.json), each test changing one thing on one side.
/// </summary>
public sealed class SchemaCheckerTests
{
    private static readonly Guid TestPage = new("e245cdaa-4176-4551-a6c4-ff3ead958d31");
    private static readonly Guid VariantPage = new("4219aaf3-5468-437f-a14f-a2980f650693");
    private static readonly Guid TextBlock = new("f367da27-ad0a-41fd-a6d5-8f7f908da4a5");
    private static readonly Guid Textstring = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid BlockList = new("4277e68b-179a-4625-bb22-5bb1df778b2d");
    private static readonly Guid ImageType = new("cc07b313-0843-4aa8-bbda-871c8da728c8");
    private static readonly Guid FolderType = new("f38bd2d7-65d0-48e6-95dc-87ce06ec2d3d");
    private static readonly Guid FileType = new("4c52d8ab-54e6-40cd-999c-7a5f24903e4d");
    private static readonly Guid ImageCropper = new("1df9f033-e6d4-451f-b8d2-e0cbc50a836f");

    // The schema as the test site serves it, by kind and key.
    private static Dictionary<(SchemaItemKind Kind, Guid Key), JsonObject> Schema()
    {
        var items = new Dictionary<(SchemaItemKind, Guid), JsonObject>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema"), "*.json"))
        {
            var item = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            var name = Path.GetFileName(file);
            var kind = name.StartsWith("document-type-", StringComparison.Ordinal) ? SchemaItemKind.DocumentType
                : name.StartsWith("media-type-", StringComparison.Ordinal) ? SchemaItemKind.MediaType
                : SchemaItemKind.DataType;
            items[(kind, Guid.Parse((string)item["id"]!))] = item;
        }

        return items;
    }

    private sealed class Side(Dictionary<(SchemaItemKind Kind, Guid Key), JsonObject> items)
    {
        public List<(SchemaItemKind Kind, Guid Key)> Reads { get; } = [];

        public Dictionary<(SchemaItemKind Kind, Guid Key), JsonObject> Items => items;

        public Task<string?> ReadAsync(SchemaItemKind kind, Guid key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Reads.Add((kind, key));
            return Task.FromResult(items.TryGetValue((kind, key), out var item) ? item.ToJsonString() : null);
        }
    }

    private static async Task<(SchemaCheck Check, Side Local, Side Remote)> Check(Guid documentType, Action<Side>? editRemote = null, Action<Side>? editLocal = null)
    {
        var local = new Side(Schema());
        var remote = new Side(Schema());
        editLocal?.Invoke(local);
        editRemote?.Invoke(remote);
        var check = await SchemaChecker.CheckAsync(documentType, local.ReadAsync, remote.ReadAsync, CancellationToken.None);
        return (check, local, remote);
    }

    private static async Task<(SchemaCheck Check, Side Local, Side Remote)> CheckMedia(Guid mediaType, Action<Side>? editRemote = null, Action<Side>? editLocal = null)
    {
        var local = new Side(Schema());
        var remote = new Side(Schema());
        editLocal?.Invoke(local);
        editRemote?.Invoke(remote);
        var check = await SchemaChecker.CheckMediaTypeAsync(mediaType, local.ReadAsync, remote.ReadAsync, CancellationToken.None);
        return (check, local, remote);
    }

    private static JsonObject MediaType(Side side, Guid key) => side.Items[(SchemaItemKind.MediaType, key)];

    private static JsonObject MediaProperty(Side side, Guid type, string alias)
        => MediaType(side, type)["properties"]!.AsArray().OfType<JsonObject>().Single(p => (string?)p["alias"] == alias);

    private static JsonObject Type(Side side, Guid key) => side.Items[(SchemaItemKind.DocumentType, key)];

    private static JsonObject Property(Side side, Guid type, string alias)
        => Type(side, type)["properties"]!.AsArray().OfType<JsonObject>().Single(p => (string?)p["alias"] == alias);

    [Theory]
    [InlineData("e245cdaa-4176-4551-a6c4-ff3ead958d31")]
    [InlineData("4219aaf3-5468-437f-a14f-a2980f650693")]
    public async Task CheckAsync_TheSameSchemaOnBothSides_IsOk(string documentType)
    {
        var (check, _, _) = await Check(Guid.Parse(documentType));

        Assert.True(check.IsOk);
        Assert.Empty(check.Problems);
    }

    // Umbraco ignores a pattern that is null, empty or white space; one environment served "" where its copy served null.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckAsync_AnEmptyPatternOnOneSide_IsNoPattern(string pattern)
    {
        var (check, _, _) = await Check(TestPage, side => Property(side, TestPage, "title")["validation"]!["regEx"] = pattern);

        Assert.True(check.IsOk);
    }

    [Fact]
    public async Task CheckAsync_APatternOnOneSideOnly_Differs()
    {
        var (check, _, _) = await Check(TestPage, side => Property(side, TestPage, "title")["validation"]!["regEx"] = "^[a-z]+$");

        var problem = Assert.Single(check.Problems);
        Assert.Equal(SchemaProblemReason.Differs, problem.Reason);
        Assert.Equal(["property 'title' pattern: none here, ^[a-z]+$ on the remote"], problem.Details);
    }

    [Fact]
    public async Task CheckAsync_FollowsDataTypesAndTheElementTypesInBlocks_EachOnce()
    {
        var (_, local, _) = await Check(TestPage);

        // The page, its 7 data types, the 2 element types the block list and grid allow, and the textarea the text block
        // uses (the image block's media picker is one of the 7). Not the variant page, which the test page doesn't use.
        Assert.Equal(11, local.Reads.Count);
        Assert.DoesNotContain((SchemaItemKind.DocumentType, VariantPage), local.Reads);
        Assert.Equal(local.Reads.Count, local.Reads.Distinct().Count());
        Assert.Contains((SchemaItemKind.DocumentType, TextBlock), local.Reads);
        Assert.Contains((SchemaItemKind.DataType, BlockList), local.Reads);
    }

    [Fact]
    public async Task CheckAsync_ADocumentTypeMissingOnTheRemote_IsTheOnlyProblem()
    {
        var (check, _, remote) = await Check(TestPage, side => side.Items.Remove((SchemaItemKind.DocumentType, TestPage)));

        var problem = Assert.Single(check.Problems);
        Assert.Equal(new SchemaProblem(SchemaItemKind.DocumentType, TestPage, "Transfr Test Page", false, SchemaProblemReason.MissingOnRemote, []), problem with { Details = [] });
        Assert.Empty(problem.Details);
        Assert.Single(remote.Reads);
    }

    [Fact]
    public async Task CheckAsync_ADocumentTypeMissingHere_IsMissingLocally()
    {
        var (check, _, _) = await Check(VariantPage, editLocal: side => side.Items.Remove((SchemaItemKind.DocumentType, VariantPage)));

        Assert.Equal(SchemaProblemReason.MissingLocally, Assert.Single(check.Problems).Reason);
    }

    [Fact]
    public async Task CheckAsync_AnElementTypeMissingOnTheRemote_IsReportedAsAnElementType()
    {
        var (check, _, _) = await Check(TestPage, side => side.Items.Remove((SchemaItemKind.DocumentType, TextBlock)));

        var problem = Assert.Single(check.Problems);
        Assert.Equal((TextBlock, true, SchemaProblemReason.MissingOnRemote, "Transfr Text Block"), (problem.Key, problem.IsElement, problem.Reason, problem.Name));
    }

    [Fact]
    public async Task CheckAsync_APropertyWithAnotherDataType_Differs()
    {
        var (check, _, _) = await Check(TestPage, side => Property(side, TestPage, "title")["dataType"] = new JsonObject { ["id"] = BlockList.ToString() });

        var problem = Assert.Single(check.Problems);
        Assert.Equal(SchemaProblemReason.Differs, problem.Reason);
        Assert.Equal(["property 'title' uses another data type"], problem.Details);
    }

    [Fact]
    public async Task CheckAsync_APropertyMissingOnTheRemote_Differs()
    {
        var (check, _, _) = await Check(TestPage, side =>
        {
            var properties = Type(side, TestPage)["properties"]!.AsArray();
            properties.Remove(properties.OfType<JsonObject>().Single(p => (string?)p["alias"] == "grid"));
        });

        Assert.Equal(["property 'grid' is missing on the remote"], Assert.Single(check.Problems).Details);
    }

    [Fact]
    public async Task CheckAsync_OtherVariationOrValidation_Differs()
    {
        var (check, _, _) = await Check(VariantPage, side =>
        {
            Type(side, VariantPage)["variesByCulture"] = false;
            Property(side, VariantPage, "title")["validation"]!["mandatory"] = true;
        });

        Assert.Equal(
            ["varies by culture: yes here, no on the remote", "property 'title' is mandatory: no here, yes on the remote"],
            Assert.Single(check.Problems).Details);
    }

    [Fact]
    public async Task CheckAsync_ADataTypeWithOtherSettings_Differs()
    {
        var (check, _, _) = await Check(TestPage, side =>
            side.Items[(SchemaItemKind.DataType, BlockList)]["values"]!.AsArray().OfType<JsonObject>()
                .Single(v => (string?)v["alias"] == "validationLimit")["value"]!["max"] = 3);

        var problem = Assert.Single(check.Problems);
        Assert.Equal((SchemaItemKind.DataType, BlockList, "Transfr Test - Block List"), (problem.Kind, problem.Key, problem.Name));
        Assert.Equal(["setting 'validationLimit' differs"], problem.Details);
    }

    [Fact]
    public async Task CheckAsync_ADataTypeWithAnotherEditor_Differs()
    {
        var (check, _, _) = await Check(TestPage, side => side.Items[(SchemaItemKind.DataType, Textstring)]["editorAlias"] = "Umbraco.TextArea");

        Assert.Equal(["editor: Umbraco.TextBox here, Umbraco.TextArea on the remote"], Assert.Single(check.Problems).Details);
    }

    [Fact]
    public async Task CheckAsync_IgnoresNamesIconsDescriptionsTabsAndSortOrders()
    {
        var (check, _, _) = await Check(TestPage, side =>
        {
            var type = Type(side, TestPage);
            type["name"] = "Renamed";
            type["icon"] = "icon-other";
            type["description"] = "Other";
            type["containers"]![0]!["name"] = "Other tab";
            Property(side, TestPage, "title")["name"] = "Heading";
            Property(side, TestPage, "title")["sortOrder"] = 9;
            side.Items[(SchemaItemKind.DataType, Textstring)]["name"] = "Text line";
        });

        Assert.True(check.IsOk);
    }

    [Fact]
    public async Task CheckAsync_AMissingComposition_IsFound()
    {
        var composition = new Guid("1d2c3b4a-5f6e-4d7c-8b9a-0a1b2c3d4e5f");
        var (check, _, _) = await Check(
            VariantPage,
            editLocal: side =>
            {
                side.Items[(SchemaItemKind.DocumentType, composition)] = new JsonObject { ["id"] = composition.ToString(), ["name"] = "SEO", ["alias"] = "seo" };
                Type(side, VariantPage)["compositions"] = new JsonArray(new JsonObject { ["documentType"] = new JsonObject { ["id"] = composition.ToString() } });
            },
            editRemote: side => Type(side, VariantPage)["compositions"] = new JsonArray(new JsonObject { ["documentType"] = new JsonObject { ["id"] = composition.ToString() } }));

        var problem = Assert.Single(check.Problems);
        Assert.Equal((composition, SchemaProblemReason.MissingOnRemote, "SEO"), (problem.Key, problem.Reason, problem.Name));
    }

    [Theory]
    [InlineData("cc07b313-0843-4aa8-bbda-871c8da728c8")]
    [InlineData("f38bd2d7-65d0-48e6-95dc-87ce06ec2d3d")]
    [InlineData("4c52d8ab-54e6-40cd-999c-7a5f24903e4d")]
    public async Task CheckMediaTypeAsync_TheSameSchemaOnBothSides_IsOk(string mediaType)
    {
        var (check, _, _) = await CheckMedia(Guid.Parse(mediaType));

        Assert.True(check.IsOk);
        Assert.Equal(Guid.Parse(mediaType), check.TypeKey);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_FollowsThePropertiesDataTypes_EachOnce()
    {
        var (_, local, remote) = await CheckMedia(ImageType);

        // Width and height share a label data type.
        Assert.Equal(
            [
                (SchemaItemKind.MediaType, ImageType),
                (SchemaItemKind.DataType, ImageCropper),
                (SchemaItemKind.DataType, new Guid("5eb57825-e15e-4fc7-8e37-fca65cdafbde")),
                (SchemaItemKind.DataType, new Guid("ba5bdbe6-ab3e-46a8-82b3-2c45f10bc47f")),
                (SchemaItemKind.DataType, new Guid("f0bc4bfb-b499-40d6-ba86-058885a5178c")),
            ],
            local.Reads);
        Assert.Equal(local.Reads, remote.Reads);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_AMediaTypeMissingOnTheRemote_IsTheOnlyProblem()
    {
        var (check, _, remote) = await CheckMedia(ImageType, editRemote: side => side.Items.Remove((SchemaItemKind.MediaType, ImageType)));

        var problem = Assert.Single(check.Problems);
        Assert.Equal((SchemaItemKind.MediaType, ImageType, "Image", SchemaProblemReason.MissingOnRemote), (problem.Kind, problem.Key, problem.Name, problem.Reason));
        Assert.Equal([(SchemaItemKind.MediaType, ImageType)], remote.Reads);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_APropertyWithAnotherDataTypeOrMandatory_Differs()
    {
        var (check, _, _) = await CheckMedia(ImageType, editRemote: side =>
        {
            MediaProperty(side, ImageType, "umbracoFile")["dataType"]!["id"] = "84c6b441-31df-4ffe-b67e-67d5bc3ae65a";
            MediaProperty(side, ImageType, "umbracoBytes")["validation"]!["mandatory"] = true;
        });

        var problem = Assert.Single(check.Problems, problem => problem.Kind == SchemaItemKind.MediaType);
        Assert.Equal(
            ["property 'umbracoFile' uses another data type", "property 'umbracoBytes' is mandatory: no here, yes on the remote"],
            problem.Details);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_OtherAllowedChildMediaTypes_Differ()
    {
        var (check, _, _) = await CheckMedia(FolderType, editRemote: side => MediaType(side, FolderType)["allowedMediaTypes"]!.AsArray().RemoveAt(0));

        var problem = Assert.Single(check.Problems);
        Assert.Equal((SchemaItemKind.MediaType, FolderType), (problem.Kind, problem.Key));
        Assert.Equal(["allowed child types differ"], problem.Details);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_FollowsCompositionsAsMediaTypes()
    {
        // The remote Image is composed of File; the walk reads File as a media type, and the File type is fine.
        var (check, _, remote) = await CheckMedia(ImageType, editRemote: side => MediaType(side, ImageType)["compositions"] = new JsonArray(
            new JsonObject { ["mediaType"] = new JsonObject { ["id"] = FileType.ToString() }, ["compositionType"] = "Composition" }));

        var problem = Assert.Single(check.Problems);
        Assert.Equal(ImageType, problem.Key);
        Assert.Equal(["compositions differ"], problem.Details);
        Assert.Contains((SchemaItemKind.MediaType, FileType), remote.Reads);
    }

    [Fact]
    public async Task CheckMediaTypeAsync_ADataTypeWithOtherSettings_Differs()
    {
        var (check, _, _) = await CheckMedia(ImageType, editRemote: side => side.Items[(SchemaItemKind.DataType, ImageCropper)]["values"] = new JsonArray(
            new JsonObject { ["alias"] = "crops", ["value"] = new JsonArray(new JsonObject { ["alias"] = "square", ["width"] = 100, ["height"] = 100 }) }));

        var problem = Assert.Single(check.Problems);
        Assert.Equal((SchemaItemKind.DataType, ImageCropper), (problem.Kind, problem.Key));
    }

    [Fact]
    public async Task CheckMediaTypeAsync_ReadsNoTemplates()
    {
        // A media type has no templates; stray template fields (as on a document type) must not count.
        var (check, _, _) = await CheckMedia(ImageType, editRemote: side =>
        {
            MediaType(side, ImageType)["allowedTemplates"] = new JsonArray(new JsonObject { ["id"] = Guid.NewGuid().ToString() });
            MediaType(side, ImageType)["defaultTemplate"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() };
            MediaType(side, ImageType)["allowedDocumentTypes"] = new JsonArray(new JsonObject { ["documentType"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() } });
        });

        Assert.True(check.IsOk);
    }
}