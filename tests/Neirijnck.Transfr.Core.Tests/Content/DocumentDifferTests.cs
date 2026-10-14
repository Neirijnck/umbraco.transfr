using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Tests.Content;

/// <summary>
/// Runs against <c>GET document/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/document-*.json),
/// each test changing one thing on one side.
/// </summary>
public sealed class DocumentDifferTests
{
    private const string Features = "document-features.json";
    private const string VariantPage = "document-variant-page.json";
    private const string DraftPage = "document-draft-page.json";

    private static readonly Guid FirstBlock = new("87ce82be-b9f6-451c-af01-12ac68ca635b");
    private static readonly Guid SecondBlock = new("843e05e6-1a53-4c00-956a-3ee09154c9af");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var document = JsonNode.Parse(Fixture(name))!.AsObject();
        edit(document);
        return document.ToJsonString();
    }

    private static JsonObject ValueOf(JsonObject document, string alias, string? culture = null)
        => document["values"]!.AsArray().OfType<JsonObject>().Single(entry => (string?)entry["alias"] == alias && (string?)entry["culture"] == culture);

    private static DocumentDiff Diff(string name, Action<JsonObject> edit) => DocumentDiffer.Diff(Fixture(name), Edited(name, edit));

    private static PropertyDiff Property(DocumentDiff diff, string alias, string? culture = null)
        => diff.Properties.Single(property => property.Alias == alias && property.Culture == culture);

    [Theory]
    [InlineData(Features)]
    [InlineData(VariantPage)]
    [InlineData(DraftPage)]
    public void Diff_TheSameDocument_IsIdenticalAndListsEverything(string name)
    {
        var diff = DocumentDiffer.Diff(Fixture(name), Fixture(name));
        var values = JsonNode.Parse(Fixture(name))!["values"]!.AsArray().Count;

        Assert.True(diff.IsIdentical);
        Assert.Equal(values, diff.Properties.Count);
        Assert.Contains(diff.Fields, field => field.Kind == ContentDifferenceKind.DocumentType && field.Local is not null);
    }

    [Fact]
    public void Diff_ShowsAChangedTextOnBothSides_AndNothingElseDiffers()
    {
        var diff = Diff(Features, document => ValueOf(document, "title")["value"] = "Features, edited");

        var title = Property(diff, "title");
        Assert.True(title.Value.Differs);
        Assert.Equal("Features", title.Value.Local);
        Assert.Equal("Features, edited", title.Value.Remote);
        Assert.Single(diff.Properties, property => property.Value.Differs);
        Assert.All(diff.Fields, field => Assert.False(field.Differs));
    }

    [Fact]
    public void Diff_ShowsRichTextAsItsMarkup()
    {
        var diff = Diff(Features, document => ValueOf(document, "body")["value"]!["markup"] = "<p>Other.</p>");

        var body = Property(diff, "body");
        Assert.True(body.Value.Differs);
        Assert.StartsWith("<p>See <a href=", body.Value.Local, StringComparison.Ordinal);
        Assert.Equal("<p>Other.</p>", body.Value.Remote);
        Assert.Empty(body.Value.Blocks);
    }

    [Fact]
    public void Diff_ShowsBlocksOneByOne_WithTheChangedValueInside()
    {
        var diff = Diff(Features, document =>
            ValueOf(document, "blocks")["value"]!["contentData"]![0]!["values"]![0]!["value"] = "First block, edited");

        var blocks = Property(diff, "blocks").Value;
        Assert.True(blocks.Differs);
        Assert.Null(blocks.Local);
        Assert.Equal([FirstBlock, SecondBlock], blocks.Blocks.Select(block => block.Key));
        Assert.Equal([BlockDiffStatus.Changed, BlockDiffStatus.Same], blocks.Blocks.Select(block => block.Status));

        var text = Assert.Single(blocks.Blocks[0].Values);
        Assert.Equal("text", text.Alias);
        Assert.Equal(("First block", "First block, edited"), (text.Value.Local, text.Value.Remote));
        Assert.Equal(new Guid("f367da27-ad0a-41fd-a6d5-8f7f908da4a5"), blocks.Blocks[0].ContentTypeKey);
    }

    [Fact]
    public void Diff_MarksSwappedBlocksAsMoved_WithTheirPlaceOnEachSide()
    {
        var diff = Diff(Features, document =>
        {
            var layout = ValueOf(document, "blocks")["value"]!["layout"]!.AsObject();
            var items = layout["Umbraco.BlockList"]!.AsArray().Select(item => item!.DeepClone()).Reverse().ToArray();
            layout["Umbraco.BlockList"] = new JsonArray(items);
        });

        var blocks = Property(diff, "blocks").Value;
        Assert.True(blocks.Differs);
        Assert.Equal([BlockDiffStatus.Moved, BlockDiffStatus.Same], blocks.Blocks.Select(block => block.Status));
        Assert.Equal((0, 1), (blocks.Blocks[0].LocalIndex, blocks.Blocks[0].RemoteIndex));
    }

    [Fact]
    public void Diff_ShowsABlockThatIsOnOneSideOnly()
    {
        var diff = Diff(Features, document =>
        {
            var blocks = ValueOf(document, "blocks")["value"]!.AsObject();
            blocks["layout"]!["Umbraco.BlockList"]!.AsArray().RemoveAt(1);
            blocks["contentData"]!.AsArray().RemoveAt(1);
            blocks["expose"]!.AsArray().RemoveAt(1);
        });

        var block = Property(diff, "blocks").Value.Blocks.Single(b => b.Key == SecondBlock);
        Assert.Equal(BlockDiffStatus.OnlyLocal, block.Status);
        Assert.Equal((1, (int?)null), (block.LocalIndex, block.RemoteIndex));
        Assert.Null(Assert.Single(block.Values).Value.Remote);
    }

    [Fact]
    public void Diff_ShowsBlockSettings_WithTheirElementType()
    {
        var settingsType = new Guid("5b6f0e2a-1c3d-4e5f-8a9b-0c1d2e3f4a5b");
        static Action<JsonObject> WithSettings(Guid settingsType, string colour) => document =>
        {
            var blocks = ValueOf(document, "blocks")["value"]!.AsObject();
            blocks["layout"]!["Umbraco.BlockList"]![0]!["settingsKey"] = "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a";
            blocks["settingsData"] = new JsonArray(new JsonObject
            {
                ["contentTypeKey"] = settingsType.ToString(),
                ["key"] = "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a",
                ["values"] = new JsonArray(new JsonObject { ["editorAlias"] = "Umbraco.TextBox", ["alias"] = "colour", ["culture"] = null, ["segment"] = null, ["value"] = colour }),
            });
        };

        var diff = DocumentDiffer.Diff(Edited(Features, WithSettings(settingsType, "red")), Edited(Features, WithSettings(settingsType, "blue")));

        var block = Property(diff, "blocks").Value.Blocks[0];
        Assert.Equal(BlockDiffStatus.Changed, block.Status);
        Assert.Equal(settingsType, block.SettingsTypeKey);
        Assert.All(block.Values, value => Assert.False(value.Value.Differs));
        var colour = Assert.Single(block.Settings);
        Assert.Equal(("red", "blue"), (colour.Value.Local, colour.Value.Remote));
    }

    [Fact]
    public void Diff_ShowsAGridBlockToo()
    {
        var diff = Diff(Features, document => ValueOf(document, "grid")["value"]!["contentData"]![0]!["values"]![0]!["value"] = "Grid, edited");

        var block = Assert.Single(Property(diff, "grid").Value.Blocks);
        Assert.Equal(BlockDiffStatus.Changed, block.Status);
    }

    [Fact]
    public void Diff_ShowsOtherValuesAsJsonWithoutWhatTheComparisonIgnores()
    {
        var diff = Diff(Features, document => ValueOf(document, "links")["value"]![0]!["url"] = "/about-us/");

        var links = Property(diff, "links").Value;
        Assert.False(links.Differs);
        Assert.Equal(links.Local, links.Remote);
        Assert.DoesNotContain("about-us", links.Remote, StringComparison.Ordinal);
        Assert.Contains("\"https://umbraco.com\"", links.Local, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_ShowsNameAndStatePerCulture()
    {
        var diff = Diff(VariantPage, document =>
            document["variants"]!.AsArray().OfType<JsonObject>().Single(v => (string?)v["culture"] == "nl-BE")["name"] = "Andere naam");

        var names = diff.Fields.Where(field => field.Kind == ContentDifferenceKind.Name).ToList();
        Assert.Equal(["en-US", "nl-BE"], names.Select(name => name.Culture));
        Assert.Equal([false, true], names.Select(name => name.Differs));
        Assert.Equal(("Variante Pagina", "Andere naam"), (names[1].Local, names[1].Remote));
    }

    [Fact]
    public void Diff_ShowsAValueOnOneSideOnlyWithNullOnTheOther()
    {
        var diff = Diff(DraftPage, document => document["values"]!.AsArray().Clear());

        var title = Property(diff, "title");
        Assert.True(title.Value.Differs);
        Assert.Equal(("Draft Page", (string?)null), (title.Value.Local, title.Value.Remote));
    }

    public static TheoryData<string, Action<JsonObject>> Edits => new()
    {
        { Features, d => ValueOf(d, "title")["value"] = "x" },
        { Features, d => ValueOf(d, "body")["value"]!["markup"] = "<p>x</p>" },
        { Features, d => ValueOf(d, "links")["value"]![1]!["url"] = "https://example.com" },
        { Features, d => ValueOf(d, "links")["value"]![0]!["url"] = "/derived/" },
        { Features, d => ValueOf(d, "blocks")["value"]!["contentData"]![1]!["values"]![0]!["value"]![0]!["focalPoint"] = new JsonObject { ["left"] = 0.5 } },
        { Features, d => d["template"] = new JsonObject { ["id"] = "6c1a3a4e-2f4b-4d6e-9f1a-0b2c3d4e5f60" } },
        { VariantPage, d => ValueOf(d, "body", "nl-BE")["value"]!["markup"] = "<p>x</p>" },
        { VariantPage, d => d["variants"]![1]!["state"] = "Draft" },
        { DraftPage, d => d["values"]!.AsArray().Clear() },
    };

    [Theory]
    [MemberData(nameof(Edits))]
    public void Diff_DiffersExactlyWhereTheComparerSeesADifference(string name, Action<JsonObject> edit)
    {
        var local = Fixture(name);
        var remote = Edited(name, edit);

        var diff = DocumentDiffer.Diff(local, remote);
        var fromDiff = diff.Fields.Where(field => field.Differs).Select(field => new ContentDifference(field.Kind, null, field.Culture, field.Segment))
            .Concat(diff.Properties.Where(property => property.Value.Differs).Select(property => new ContentDifference(ContentDifferenceKind.Value, property.Alias, property.Culture, property.Segment)));

        Assert.Equal(ContentComparer.Compare(local, remote).Differences.OrderBy(d => d.ToString()), fromDiff.OrderBy(d => d.ToString()));
        Assert.Equal(ContentComparer.Compare(local, remote).IsIdentical, diff.IsIdentical);
    }
}