using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.References;

namespace Neirijnck.Transfr.Core.Tests.References;

/// <summary>
/// Runs against <c>GET document/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/document-*.json)
/// and values in the editor shapes measured there.
/// </summary>
public sealed class ReferenceExtractorTests
{
    private static readonly Guid About = new("924c1efa-9b3d-4575-9106-166e6ed39ad1");
    private static readonly Guid Red = new("d7055f49-d608-40a3-94d4-d4b5cbd0facb");
    private static readonly Guid Blue = new("a28038ed-b11a-41f6-a990-bed021cac2fb");
    private static readonly Guid Member = new("5f0e1d2c-3b4a-4958-8776-655443322110");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // A document with one property of the given editor and value.
    private static IReadOnlyList<ContentReference> ExtractValue(string editorAlias, JsonNode? value, string? culture = null)
        => ReferenceExtractor.Extract(new JsonObject
        {
            ["values"] = new JsonArray(new JsonObject
            {
                ["editorAlias"] = editorAlias,
                ["alias"] = "picked",
                ["culture"] = culture,
                ["segment"] = null,
                ["value"] = value,
            }),
        }.ToJsonString());

    [Fact]
    public void Extract_FindsEveryKindOfReferenceInTheFeaturesPage()
    {
        var references = ReferenceExtractor.Extract(Fixture("document-features.json"));

        Assert.Equal(
            [
                new ContentReference(ReferenceKind.Document, About, "body", null, null),
                new ContentReference(ReferenceKind.Media, Red, "body", null, null),
                new ContentReference(ReferenceKind.Document, About, "related", null, null),
                new ContentReference(ReferenceKind.Document, About, "links", null, null),
                new ContentReference(ReferenceKind.Media, Red, "image", null, null),
                new ContentReference(ReferenceKind.Media, Blue, "blocks", null, null),
            ],
            references);
    }

    [Theory]
    [InlineData("document-draft-page.json")]
    [InlineData("document-variant-page.json")]
    public void Extract_FindsNothingInTextOnlyPages(string name)
    {
        Assert.Empty(ReferenceExtractor.Extract(Fixture(name)));
    }

    [Fact]
    public void Extract_ReadsATreePickerByType_DocumentMediaAndMember()
    {
        var value = new JsonArray(
            new JsonObject { ["type"] = "document", ["unique"] = About.ToString() },
            new JsonObject { ["type"] = "media", ["unique"] = Red.ToString() },
            new JsonObject { ["type"] = "member", ["unique"] = Member.ToString() },
            new JsonObject { ["type"] = "something-else", ["unique"] = Blue.ToString() });

        var references = ExtractValue("Umbraco.MultiNodeTreePicker", value);

        Assert.Equal([(ReferenceKind.Document, About), (ReferenceKind.Media, Red), (ReferenceKind.Member, Member)], references.Select(r => (r.Kind, r.Key)));
    }

    [Fact]
    public void Extract_ReadsAStoredListOfUdisToo()
    {
        var references = ExtractValue("Umbraco.MultiNodeTreePicker", $"umb://document/{About:N},umb://media/{Red:N}");

        Assert.Equal([(ReferenceKind.Document, About), (ReferenceKind.Media, Red)], references.Select(r => (r.Kind, r.Key)));
    }

    [Theory]
    [InlineData("5f0e1d2c-3b4a-4958-8776-655443322110")]
    [InlineData("umb://member/5f0e1d2c3b4a49588776655443322110")]
    public void Extract_ReadsAMemberPickerByKeyOrUdi(string value)
    {
        Assert.Equal([(ReferenceKind.Member, Member)], ExtractValue("Umbraco.MemberPicker", value).Select(r => (r.Kind, r.Key)));
    }

    [Fact]
    public void Extract_ReadsRichTextLinksByTypeOrUdi_AndSkipsALinkWithoutEither()
    {
        var markup = $"""
            <p><a href="/{"{"}localLink:{About}{"}"}" TYPE="Document">a</a>
            <a href="/{"{"}localLink:umb://media/{Red:N}{"}"}">b</a>
            <a href="/{"{"}localLink:{Blue}{"}"}">no type</a>
            <a href="https://example.com">external</a></p>
            """;

        var references = ExtractValue("Umbraco.RichText", new JsonObject { ["markup"] = markup, ["blocks"] = null });

        Assert.Equal([(ReferenceKind.Document, About), (ReferenceKind.Media, Red)], references.Select(r => (r.Kind, r.Key)));
    }

    [Fact]
    public void Extract_FollowsBlocksInsideBlocks()
    {
        var inner = new JsonObject
        {
            ["layout"] = new JsonObject(),
            ["contentData"] = new JsonArray(new JsonObject
            {
                ["values"] = new JsonArray(new JsonObject { ["editorAlias"] = "Umbraco.ContentPicker", ["alias"] = "link", ["value"] = About.ToString() }),
            }),
        };
        var outer = new JsonObject
        {
            ["layout"] = new JsonObject(),
            ["contentData"] = new JsonArray(new JsonObject
            {
                ["values"] = new JsonArray(new JsonObject { ["editorAlias"] = "Umbraco.BlockList", ["alias"] = "nested", ["value"] = inner }),
            }),
            ["settingsData"] = new JsonArray(new JsonObject
            {
                ["values"] = new JsonArray(new JsonObject { ["editorAlias"] = "Umbraco.MediaPicker3", ["alias"] = "background", ["value"] = new JsonArray(new JsonObject { ["mediaKey"] = Red.ToString() }) }),
            }),
        };

        var references = ExtractValue("Umbraco.BlockList", outer);

        Assert.Equal([(ReferenceKind.Document, About), (ReferenceKind.Media, Red)], references.Select(r => (r.Kind, r.Key)));
        Assert.All(references, reference => Assert.Equal("picked", reference.PropertyAlias));
    }

    [Fact]
    public void Extract_SearchesAnUnknownEditorForUdis()
    {
        var value = new JsonObject { ["target"] = $"umb://document/{About:N}", ["note"] = "no key here 924c1efa" };

        Assert.Equal([(ReferenceKind.Document, About)], ExtractValue("Acme.CustomPicker", value).Select(r => (r.Kind, r.Key)));
    }

    [Fact]
    public void Extract_KeepsTheCultureAndListsAReferenceOncePerValue()
    {
        var references = ExtractValue("Umbraco.MultiNodeTreePicker", $"umb://document/{About:N},umb://document/{About:N}", "nl-BE");

        Assert.Equal([new ContentReference(ReferenceKind.Document, About, "picked", "nl-BE", null)], references);
    }
}