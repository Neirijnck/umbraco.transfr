using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Tests.Content;

/// <summary>
/// Runs against <c>GET media/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/media-*.json).
/// </summary>
public sealed class MediaDifferTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var media = JsonNode.Parse(Fixture(name))!.AsObject();
        edit(media);
        return media.ToJsonString();
    }

    private static JsonObject ValueOf(JsonObject media, string alias)
        => media["values"]!.AsArray().OfType<JsonObject>().Single(entry => (string?)entry["alias"] == alias);

    [Fact]
    public void Diff_LaysOutTheMediaTypeAndName_WithoutTemplateOrPublishState()
    {
        var diff = MediaDiffer.Diff(Fixture("media-image.json"), Fixture("media-image.json"));

        Assert.True(diff.IsIdentical);
        Assert.Equal(
            [(ContentDifferenceKind.MediaType, "cc07b313-0843-4aa8-bbda-871c8da728c8"), (ContentDifferenceKind.Name, "Red")],
            diff.Fields.Select(field => (field.Kind, field.Local)));
        Assert.Equal(["umbracoFile", "umbracoWidth", "umbracoHeight", "umbracoBytes", "umbracoExtension"], diff.Properties.Select(property => property.Alias));
    }

    [Fact]
    public void Diff_ShowsAChangedFocalPoint_AndTheNameOnBothSides()
    {
        var remote = Edited("media-image.json", media =>
        {
            ValueOf(media, "umbracoFile")["value"]!["focalPoint"] = new JsonObject { ["left"] = 0.2, ["top"] = 0.3 };
            media["variants"]![0]!["name"] = "Red (B)";
        });

        var diff = MediaDiffer.Diff(Fixture("media-image.json"), remote);

        Assert.False(diff.IsIdentical);
        var name = Assert.Single(diff.Fields, field => field.Kind == ContentDifferenceKind.Name);
        Assert.Equal(("Red", "Red (B)", true), (name.Local, name.Remote, name.Differs));
        var file = Assert.Single(diff.Properties, property => property.Value.Differs);
        Assert.Equal("umbracoFile", file.Alias);
        Assert.Contains("\"left\": 0.2", file.Value.Remote, StringComparison.Ordinal);
        Assert.Contains("\"src\": \"/media/zn1jr2zj/red.png\"", file.Value.Local, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_DecidesWhatDiffersAsTheComparerDoes()
    {
        var remote = Edited("media-image.json", media => ValueOf(media, "umbracoBytes")["value"] = "3866");

        var diff = MediaDiffer.Diff(Fixture("media-image.json"), remote);
        var comparison = MediaComparer.Compare(Fixture("media-image.json"), remote);

        Assert.Equal(
            comparison.Differences.Select(difference => difference.Alias),
            diff.Properties.Where(property => property.Value.Differs).Select(property => property.Alias));
    }

    [Fact]
    public void Of_NamesEveryFileWithAPath()
    {
        Assert.Equal([new MediaFile("umbracoFile", null, null, "/media/zn1jr2zj/red.png")], MediaFiles.Of(Fixture("media-image.json")));
        Assert.Equal([new MediaFile("umbracoFile", null, null, "/media/2fhdb0lj/probe.txt")], MediaFiles.Of(Fixture("media-file.json")));
        Assert.Empty(MediaFiles.Of(Fixture("media-folder.json")));
        Assert.Empty(MediaFiles.Of(Edited("media-image.json", media => ValueOf(media, "umbracoFile")["value"]!["src"] = string.Empty)));
    }
}