using System.Text.Json;
using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Tests.Content;

/// <summary>
/// Runs against <c>GET document/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/document-*.json),
/// each test changing one thing on one side.
/// </summary>
public sealed class ContentComparerTests
{
    private const string Features = "document-features.json";
    private const string VariantPage = "document-variant-page.json";
    private const string DraftPage = "document-draft-page.json";

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var document = JsonNode.Parse(Fixture(name))!.AsObject();
        edit(document);
        return document.ToJsonString();
    }

    private static JsonObject ValueOf(JsonObject document, string alias, string? culture = null)
        => document["values"]!.AsArray()
            .OfType<JsonObject>()
            .Single(entry => (string?)entry["alias"] == alias && (string?)entry["culture"] == culture);

    private static JsonArray Reversed(JsonArray array)
    {
        var items = array.Select(item => item?.DeepClone()).Reverse().ToArray();
        return new JsonArray(items);
    }

    private static IReadOnlyList<ContentDifference> Differences(string name, Action<JsonObject> edit)
        => ContentComparer.Compare(Fixture(name), Edited(name, edit)).Differences;

    [Theory]
    [InlineData(Features)]
    [InlineData(VariantPage)]
    [InlineData(DraftPage)]
    public void Compare_TheSameDocument_IsIdentical(string name)
    {
        var comparison = ContentComparer.Compare(Fixture(name), Fixture(name));

        Assert.True(comparison.IsIdentical);
        Assert.Empty(comparison.Differences);
    }

    [Fact]
    public void Compare_IgnoresDatesFlagsIconsAndIdsThatAreNotContent()
    {
        var differences = Differences(VariantPage, document =>
        {
            document["flags"] = new JsonArray(new JsonObject { ["alias"] = "Umb.Flag" });
            document["documentType"]!["icon"] = "icon-other";
            foreach (var variant in document["variants"]!.AsArray().OfType<JsonObject>())
            {
                variant["id"] = Guid.NewGuid().ToString();
                variant["createDate"] = "2030-01-01T00:00:00+00:00";
                variant["updateDate"] = "2030-01-01T00:00:00+00:00";
                variant["publishDate"] = "2030-01-01T00:00:00+00:00";
                variant["flags"] = new JsonArray(new JsonObject { ["alias"] = "Umb.Flag" });
            }
        });

        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_IgnoresTheOrderOfValuesAndOfPropertiesInAnObject()
    {
        var differences = Differences(Features, document =>
        {
            document["values"] = Reversed(document["values"]!.AsArray());
            var link = document["values"]!.AsArray().OfType<JsonObject>().Single(entry => (string?)entry["alias"] == "links")["value"]![0]!.AsObject();
            var properties = link.Select(property => (property.Key, Value: property.Value?.DeepClone())).Reverse().ToList();
            link.Clear();
            foreach (var (key, value) in properties)
            {
                link[key] = value;
            }
        });

        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_IgnoresTheOrderOfBlockDataAndExpose()
    {
        var differences = Differences(Features, document =>
        {
            var blocks = ValueOf(document, "blocks")["value"]!.AsObject();
            blocks["contentData"] = Reversed(blocks["contentData"]!.AsArray());
            blocks["expose"] = Reversed(blocks["expose"]!.AsArray());
        });

        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_SeesTheOrderOfBlocksOnThePage()
    {
        var differences = Differences(Features, document =>
        {
            var layout = ValueOf(document, "blocks")["value"]!["layout"]!.AsObject();
            layout["Umbraco.BlockList"] = Reversed(layout["Umbraco.BlockList"]!.AsArray());
        });

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "blocks")], differences);
    }

    [Fact]
    public void Compare_SeesAValueChangedInsideABlock()
    {
        var differences = Differences(Features, document =>
        {
            var block = ValueOf(document, "blocks")["value"]!["contentData"]!.AsArray().OfType<JsonObject>().First();
            block["values"]![0]!["value"] = "First block, edited";
        });

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "blocks")], differences);
    }

    [Fact]
    public void Compare_IgnoresWhatALinkToADocumentDerivesFromThatDocument()
    {
        var differences = Differences(Features, document =>
        {
            var link = ValueOf(document, "links")["value"]![0]!.AsObject();
            link["url"] = "/about-us/";
            link["icon"] = "icon-other";
            link["published"] = false;
            link["trashed"] = true;
        });

        Assert.Empty(differences);
    }

    [Theory]
    [InlineData(0, "unique", "50c8565d-d31d-418a-a0f5-879e274a78dd")]
    [InlineData(0, "name", "About us")]
    [InlineData(1, "url", "https://example.com")]
    [InlineData(1, "target", null)]
    public void Compare_SeesWhatALinkStores(int index, string field, string? value)
    {
        var differences = Differences(Features, document => ValueOf(document, "links")["value"]![index]![field] = value);

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "links")], differences);
    }

    [Fact]
    public void Compare_DerivedFieldsAreOnlyIgnoredInALinkPicker()
    {
        // A link-shaped value of another editor (here a text area inside a block) is content as a whole, url included.
        static string WithUrl(string url) => Edited(Features, document =>
        {
            var block = ValueOf(document, "blocks")["value"]!["contentData"]!.AsArray().OfType<JsonObject>().First();
            block["values"]![0]!["value"] = new JsonArray(new JsonObject { ["type"] = "document", ["url"] = url });
        });

        var differences = ContentComparer.Compare(WithUrl("/a/"), WithUrl("/b/")).Differences;

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "blocks")], differences);
    }

    [Fact]
    public void Compare_ReportsExactlyTheChangedValue()
    {
        var differences = Differences(Features, document => ValueOf(document, "title")["value"] = "Features, edited");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "title")], differences);
    }

    [Fact]
    public void Compare_SeesChangedRichTextMarkup()
    {
        var differences = Differences(Features, document => ValueOf(document, "body")["value"]!["markup"] = "<p>Other.</p>");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "body")], differences);
    }

    [Fact]
    public void Compare_SeesAChangedMediaPickerCrop()
    {
        var differences = Differences(Features, document =>
            ValueOf(document, "image")["value"]![0]!["focalPoint"] = new JsonObject { ["left"] = 0.2, ["top"] = 0.3 });

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "image")], differences);
    }

    [Fact]
    public void Compare_ReportsAValueChangeInOneCultureOnly()
    {
        var differences = Differences(VariantPage, document => ValueOf(document, "body", "nl-BE")["value"]!["markup"] = "<p>Andere tekst.</p>");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "body", "nl-BE")], differences);
    }

    [Fact]
    public void Compare_ReportsANameChangeInOneCultureOnly()
    {
        var differences = Differences(VariantPage, document =>
            document["variants"]!.AsArray().OfType<JsonObject>().Single(v => (string?)v["culture"] == "nl-BE")["name"] = "Andere naam");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name, Culture: "nl-BE")], differences);
    }

    [Fact]
    public void Compare_ReportsACultureThatExistsOnOneSideOnly()
    {
        var differences = Differences(VariantPage, document =>
        {
            var variants = document["variants"]!.AsArray();
            variants.Remove(variants.OfType<JsonObject>().Single(v => (string?)v["culture"] == "nl-BE"));
        });

        Assert.Equal(
            [
                new ContentDifference(ContentDifferenceKind.Name, Culture: "nl-BE"),
                new ContentDifference(ContentDifferenceKind.State, Culture: "nl-BE"),
            ],
            differences);
    }

    [Fact]
    public void Compare_SeesTheNameOfAnInvariantDocument()
    {
        var differences = Differences(DraftPage, document => document["variants"]![0]!["name"] = "Draft Page, renamed");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name)], differences);
    }

    [Theory]
    [InlineData("Published")]
    [InlineData("PublishedPendingChanges")]
    public void Compare_SeesThePublishState(string state)
    {
        var differences = Differences(DraftPage, document => document["variants"]![0]!["state"] = state);

        Assert.Equal([new ContentDifference(ContentDifferenceKind.State)], differences);
    }

    [Fact]
    public void Compare_SeesTheTemplateAndTheDocumentType()
    {
        var differences = Differences(DraftPage, document =>
        {
            document["template"] = new JsonObject { ["id"] = "6c1a3a4e-2f4b-4d6e-9f1a-0b2c3d4e5f60" };
            document["documentType"]!["id"] = "4219aaf3-5468-437f-a14f-a2980f650693";
        });

        Assert.Equal(
            [new ContentDifference(ContentDifferenceKind.DocumentType), new ContentDifference(ContentDifferenceKind.Template)],
            differences);
    }

    [Fact]
    public void Compare_TreatsAMissingValueAsAnEmptyOne()
    {
        var withNull = Edited(DraftPage, document => document["values"]!.AsArray().Add(
            new JsonObject { ["editorAlias"] = "Umbraco.TextBox", ["alias"] = "subtitle", ["culture"] = null, ["segment"] = null, ["value"] = null }));

        Assert.True(ContentComparer.Compare(Fixture(DraftPage), withNull).IsIdentical);
        Assert.True(ContentComparer.Compare(withNull, Fixture(DraftPage)).IsIdentical);
    }

    [Fact]
    public void Compare_SeesAValueThatIsOnOneSideOnly()
    {
        var differences = Differences(DraftPage, document => document["values"]!.AsArray().Clear());

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "title")], differences);
    }

    [Fact]
    public void Compare_KeepsTheOrderOfAnEditorsOwnList()
    {
        // Only block data, values and expose entries are unordered; any other list (here: tags) keeps its order.
        var local = Edited(DraftPage, document => document["values"]!.AsArray().Add(
            new JsonObject { ["editorAlias"] = "Umbraco.Tags", ["alias"] = "tags", ["culture"] = null, ["segment"] = null, ["value"] = new JsonArray("a", "b") }));
        var remote = Edited(DraftPage, document => document["values"]!.AsArray().Add(
            new JsonObject { ["editorAlias"] = "Umbraco.Tags", ["alias"] = "tags", ["culture"] = null, ["segment"] = null, ["value"] = new JsonArray("b", "a") }));

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "tags")], ContentComparer.Compare(local, remote).Differences);
    }

    [Fact]
    public void Compare_RefusesWhatIsNotADocument()
    {
        Assert.Throws<ArgumentException>(() => ContentComparer.Compare("[]", Fixture(DraftPage)));
        Assert.ThrowsAny<JsonException>(() => ContentComparer.Compare(Fixture(DraftPage), "{"));
    }
}