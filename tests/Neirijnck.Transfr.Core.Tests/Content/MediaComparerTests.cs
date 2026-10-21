using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Tests.Content;

/// <summary>
/// Runs against <c>GET media/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/media-*.json): a
/// folder, an image (image cropper) and a file (upload field), each test changing one thing on one side.
/// </summary>
public sealed class MediaComparerTests
{
    private const string Folder = "media-folder.json";
    private const string Image = "media-image.json";
    private const string TextFile = "media-file.json";

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var media = JsonNode.Parse(Fixture(name))!.AsObject();
        edit(media);
        return media.ToJsonString();
    }

    private static JsonObject ValueOf(JsonObject media, string alias)
        => media["values"]!.AsArray().OfType<JsonObject>().Single(entry => (string?)entry["alias"] == alias);

    private static IReadOnlyList<ContentDifference> Differences(string name, Action<JsonObject> edit)
        => MediaComparer.Compare(Fixture(name), Edited(name, edit)).Differences;

    [Theory]
    [InlineData(Folder)]
    [InlineData(Image)]
    [InlineData(TextFile)]
    public void Compare_TheSameMedia_IsIdentical(string name)
    {
        var comparison = MediaComparer.Compare(Fixture(name), Fixture(name));

        Assert.True(comparison.IsIdentical);
    }

    [Fact]
    public void Compare_IgnoresDatesFlagsIconsCollectionsAndIdsThatAreNotContent()
    {
        var differences = Differences(Image, media =>
        {
            media["flags"] = new JsonArray(new JsonObject { ["alias"] = "Umb.Flag" });
            media["mediaType"]!["icon"] = "icon-other";
            media["mediaType"]!["collection"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() };
            media["values"] = new JsonArray([.. media["values"]!.AsArray().Select(value => value?.DeepClone()).Reverse()]);
            var variant = media["variants"]![0]!.AsObject();
            variant["createDate"] = "2030-01-01T00:00:00+00:00";
            variant["updateDate"] = "2030-01-01T00:00:00+00:00";
        });

        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_SeesAnotherMediaType()
    {
        var differences = Differences(Image, media => media["mediaType"]!["id"] = "4c52d8ab-54e6-40cd-999c-7a5f24903e4d");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.MediaType)], differences);
    }

    [Fact]
    public void Compare_SeesAnotherName()
    {
        var differences = Differences(Folder, media => media["variants"]![0]!["name"] = "Transfr Pictures");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name)], differences);
    }

    [Fact]
    public void Compare_SeesAFileUploadedUnderAnotherName()
    {
        // Measured: another file name gives another path in the same folder.
        var differences = Differences(Image, media => ValueOf(media, "umbracoFile")["value"]!["src"] = "/media/zn1jr2zj/red-2.png");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "umbracoFile")], differences);
    }

    [Fact]
    public void Compare_SeesAFileOfAnotherSizeUnderTheSameName_ThroughTheLabelsUmbracoFillsIn()
    {
        var differences = Differences(Image, media =>
        {
            ValueOf(media, "umbracoWidth")["value"] = "40";
            ValueOf(media, "umbracoHeight")["value"] = "40";
            ValueOf(media, "umbracoBytes")["value"] = "3866";
        });

        Assert.Equal(
            [
                new ContentDifference(ContentDifferenceKind.Value, "umbracoWidth"),
                new ContentDifference(ContentDifferenceKind.Value, "umbracoHeight"),
                new ContentDifference(ContentDifferenceKind.Value, "umbracoBytes"),
            ],
            differences);
    }

    [Fact]
    public void Compare_SeesAChangedFocalPointAndCrops()
    {
        var focalPoint = Differences(Image, media => ValueOf(media, "umbracoFile")["value"]!["focalPoint"] = new JsonObject { ["left"] = 0.2, ["top"] = 0.3 });
        var crops = Differences(Image, media => ValueOf(media, "umbracoFile")["value"]!["crops"] = new JsonArray(new JsonObject { ["alias"] = "square", ["width"] = 100, ["height"] = 100 }));

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "umbracoFile")], focalPoint);
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "umbracoFile")], crops);
    }

    [Fact]
    public void Compare_SeesAnUploadFieldFile()
    {
        var differences = Differences(TextFile, media => ValueOf(media, "umbracoFile")["value"]!["src"] = "/media/2fhdb0lj/other.txt");

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "umbracoFile")], differences);
    }

    [Fact]
    public void Compare_SeesValuesClearedOnOneSide()
    {
        // Measured: an update without values clears every value of a media item.
        var differences = Differences(TextFile, media => media["values"] = new JsonArray());

        Assert.Equal(
            [
                new ContentDifference(ContentDifferenceKind.Value, "umbracoFile"),
                new ContentDifference(ContentDifferenceKind.Value, "umbracoExtension"),
                new ContentDifference(ContentDifferenceKind.Value, "umbracoBytes"),
            ],
            differences);
    }

    [Fact]
    public void Compare_ReadsNoDocumentFields()
    {
        // A media item has no template or publish state; stray ones (as on a document) must not count.
        var differences = Differences(Image, media =>
        {
            media["template"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() };
            media["documentType"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() };
        });

        Assert.Empty(differences);
    }
}