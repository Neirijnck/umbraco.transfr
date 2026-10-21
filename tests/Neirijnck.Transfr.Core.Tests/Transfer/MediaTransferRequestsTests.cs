using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Core.Tests.Transfer;

/// <summary>
/// Media writes against <c>GET media/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/media-*.json),
/// in the request shapes measured there with the API user (step 10a).
/// </summary>
public sealed class MediaTransferRequestsTests
{
    private const string Api = "/umbraco/management/api/v1";

    private static readonly Guid Folder = new("e1d9deac-de16-4b2d-94fc-c81b4a0d81c6");
    private static readonly Guid Red = new("45cebd44-9840-4a3f-a411-dfebacfb497d");
    private static readonly Guid TextFile = new("49cb2712-784e-46a6-b4a0-52b7d13bf690");
    private static readonly Guid Upload = new("5a5a5a5a-5a5a-4a5a-8a5a-5a5a5a5a5a5a");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string Edited(string name, Action<JsonObject> edit)
    {
        var media = JsonNode.Parse(Fixture(name))!.AsObject();
        edit(media);
        return media.ToJsonString();
    }

    private static JsonObject ValueOf(JsonObject media, string alias)
        => media["values"]!.AsArray().OfType<JsonObject>().Single(entry => (string?)entry["alias"] == alias);

    private static PlanRow Row(Guid key, PlanOutcome outcome, Guid? parentKey = null)
        => new(PlanItemKind.Media, key, parentKey, "Media", null, outcome, [], [], [], []);

    private static JsonObject Body(MediaWrite write) => (JsonObject)JsonNode.Parse(write.Write.Body)!;

    private static MediaWrite For(PlanRow row, string source, string? target) => MediaTransferRequests.For(row, source, target, () => Upload);

    [Fact]
    public void For_CreatesAnImage_WithTheSameKeyParentAndType_UploadingItsFileUnderItsName()
    {
        var parent = Guid.NewGuid();
        var write = For(Row(Red, PlanOutcome.Create, parent), Fixture("media-image.json"), null);
        var body = Body(write);

        Assert.Equal(("POST", $"{Api}/media"), (write.Write.Method, write.Write.Path));
        Assert.Equal((Red.ToString("D"), parent.ToString("D"), "cc07b313-0843-4aa8-bbda-871c8da728c8"), ((string?)body["id"], (string?)body["parent"]!["id"], (string?)body["mediaType"]!["id"]));
        Assert.Equal([new MediaUpload(Upload, "/media/zn1jr2zj/red.png", "red.png")], write.Uploads);

        // The file value names the upload instead of a path, keeping its crops and focal point; the labels go as they are.
        var file = ValueOf(body, "umbracoFile")["value"]!.AsObject();
        Assert.Equal(Upload.ToString("D"), (string?)file["temporaryFileId"]);
        Assert.False(file.ContainsKey("src"));
        Assert.True(file.ContainsKey("crops") && file.ContainsKey("focalPoint"));
        Assert.Equal("81", (string?)ValueOf(body, "umbracoBytes")["value"]);
        Assert.Equal("Red", (string?)body["variants"]![0]!["name"]);
    }

    [Fact]
    public void For_CreatesAFolderAtTheRoot_WithNothingToUpload()
    {
        var write = For(Row(Folder, PlanOutcome.Create), Fixture("media-folder.json"), null);
        var body = Body(write);

        Assert.Empty(write.Uploads);
        Assert.Null(body["parent"]);
        Assert.Empty(body["values"]!.AsArray());
        Assert.Equal("Transfr Images", (string?)body["variants"]![0]!["name"]);
    }

    [Fact]
    public void For_UpdatesWithoutUploading_WhenOnlyTheFocalPointDiffers()
    {
        var source = Edited("media-image.json", media => ValueOf(media, "umbracoFile")["value"]!["focalPoint"] = new JsonObject { ["left"] = 0.2, ["top"] = 0.3 });
        var write = For(Row(Red, PlanOutcome.Update), source, Fixture("media-image.json"));
        var body = Body(write);

        Assert.Equal(("PUT", $"{Api}/media/{Red:D}"), (write.Write.Method, write.Write.Path));
        Assert.Empty(write.Uploads);
        Assert.Equal("/media/zn1jr2zj/red.png", (string?)ValueOf(body, "umbracoFile")["value"]!["src"]);
        Assert.Equal(0.2, (double?)ValueOf(body, "umbracoFile")["value"]!["focalPoint"]!["left"]);
        Assert.False(body.ContainsKey("id"));
    }

    [Fact]
    public void For_UploadsAFileUnderAnotherName()
    {
        var source = Edited("media-image.json", media => ValueOf(media, "umbracoFile")["value"]!["src"] = "/media/zn1jr2zj/red%20new.png");
        var write = For(Row(Red, PlanOutcome.Conflict), source, Fixture("media-image.json"));

        Assert.Equal([new MediaUpload(Upload, "/media/zn1jr2zj/red%20new.png", "red new.png")], write.Uploads);
    }

    [Fact]
    public void For_UploadsAnotherFileUnderTheSameName_SeenInTheLabelsUmbracoFillsIn()
    {
        // Measured: a 40x40 file replacing a 16x16 one under the same name keeps the path; only the labels change.
        var source = Edited("media-image.json", media =>
        {
            ValueOf(media, "umbracoWidth")["value"] = "40";
            ValueOf(media, "umbracoBytes")["value"] = "3866";
        });
        var write = For(Row(Red, PlanOutcome.Update), source, Fixture("media-image.json"));

        Assert.Equal([new MediaUpload(Upload, "/media/zn1jr2zj/red.png", "red.png")], write.Uploads);
        Assert.Equal(Upload.ToString("D"), (string?)ValueOf(Body(write), "umbracoFile")["value"]!["temporaryFileId"]);
    }

    [Fact]
    public void For_SendsAFileClearedHereAsItIs_SoItIsClearedThereToo()
    {
        var source = Edited("media-image.json", media => ValueOf(media, "umbracoFile")["value"]!["src"] = string.Empty);
        var write = For(Row(Red, PlanOutcome.Update), source, Fixture("media-image.json"));

        Assert.Empty(write.Uploads);
        Assert.Equal(string.Empty, (string?)ValueOf(Body(write), "umbracoFile")["value"]!["src"]);
    }

    [Fact]
    public void For_UploadsAnUploadFieldFile()
    {
        var write = For(Row(TextFile, PlanOutcome.Create), Fixture("media-file.json"), null);

        Assert.Equal([new MediaUpload(Upload, "/media/2fhdb0lj/probe.txt", "probe.txt")], write.Uploads);
        Assert.Equal(new JsonObject { ["temporaryFileId"] = Upload.ToString("D") }.ToJsonString(), ValueOf(Body(write), "umbracoFile")["value"]!.ToJsonString());
    }

    [Fact]
    public void For_SendsEveryValue_BecauseAnUpdateReplacesThemAll()
    {
        var write = For(Row(Red, PlanOutcome.Update), Fixture("media-image.json"), Edited("media-image.json", media => media["values"] = new JsonArray()));

        Assert.Equal(
            ["umbracoFile", "umbracoWidth", "umbracoHeight", "umbracoBytes", "umbracoExtension"],
            Body(write)["values"]!.AsArray().Select(value => (string?)value!["alias"]));
    }

    [Theory]
    [InlineData(PlanOutcome.Unchanged)]
    [InlineData(PlanOutcome.Blocked)]
    public void For_RefusesARowThatWritesNothing(PlanOutcome outcome)
    {
        Assert.Throws<ArgumentException>(() => For(Row(Red, outcome), Fixture("media-image.json"), Fixture("media-image.json")));
    }

    [Fact]
    public void For_RefusesADocumentRow_AndAnUpdateWithoutTheTarget()
    {
        var document = new PlanRow(PlanItemKind.Document, Red, null, "Page", Guid.NewGuid(), PlanOutcome.Create, [], [], [], []);

        Assert.Throws<ArgumentException>(() => For(document, Fixture("media-image.json"), null));
        Assert.Throws<ArgumentException>(() => For(Row(Red, PlanOutcome.Update), Fixture("media-image.json"), null));
    }

    [Fact]
    public void MissingFiles_NamesAFileThatCameOutEmptyThere()
    {
        var empty = Edited("media-image.json", media => ValueOf(media, "umbracoFile")["value"]!["src"] = string.Empty);

        Assert.Equal(["umbracoFile"], MediaTransferRequests.MissingFiles(Fixture("media-image.json"), empty));
        Assert.Empty(MediaTransferRequests.MissingFiles(Fixture("media-image.json"), Fixture("media-image.json")));
        Assert.Empty(MediaTransferRequests.MissingFiles(Fixture("media-folder.json"), Fixture("media-folder.json")));
    }
}