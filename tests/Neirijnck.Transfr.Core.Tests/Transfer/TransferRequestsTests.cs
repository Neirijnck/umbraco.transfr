using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Core.Tests.Transfer;

/// <summary>
/// Request shapes as measured on an Umbraco 17.7.0 test site with the API user (create/update, with or without publish,
/// invariant publish as an empty culture list, sort with the full order).
/// </summary>
public sealed class TransferRequestsTests
{
    private const string Api = "/umbraco/management/api/v1";

    private static readonly Guid Key = new("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Parent = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid PageType = new("e2450d61-29a3-4c3d-9f6e-1b9b0b3c0a01");
    private static readonly Guid Template = new("77777777-7777-4777-8777-777777777777");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static PlanRow Row(PlanOutcome outcome, params string?[] publish)
        => new(PlanItemKind.Document, Key, Parent, "Page", PageType, outcome, [], publish, [], []);

    private static JsonObject Body(DocumentWrite write) => (JsonObject)JsonNode.Parse(write.Body)!;

    private static string VariantSource()
        => new JsonObject
        {
            ["template"] = new JsonObject { ["id"] = Template.ToString() },
            ["documentType"] = new JsonObject { ["id"] = PageType.ToString() },
            ["values"] = new JsonArray(
                new JsonObject { ["alias"] = "title", ["culture"] = "en-US", ["segment"] = null, ["value"] = "en" },
                new JsonObject { ["alias"] = "title", ["culture"] = "de-DE", ["segment"] = null, ["value"] = "de" },
                new JsonObject { ["alias"] = "shared", ["culture"] = null, ["segment"] = null, ["value"] = "invariant" }),
            ["variants"] = new JsonArray(
                new JsonObject { ["culture"] = "en-US", ["segment"] = null, ["name"] = "Page en", ["state"] = "Published" },
                new JsonObject { ["culture"] = "de-DE", ["segment"] = null, ["name"] = "Page de", ["state"] = "NotCreated" }),
        }.ToJsonString();

    [Fact]
    public void For_CreatesAndPublishesInvariantContent_WithAnEmptyCultureList_KeepingKeyParentAndType()
    {
        var source = Fixture("document-features.json");

        var write = TransferRequests.For(Row(PlanOutcome.Create, [null]), source);

        Assert.Equal(("POST", $"{Api}/document/create-and-publish"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Equal(Key.ToString(), body["id"]!.GetValue<string>());
        Assert.Equal(Parent.ToString(), body["parent"]!["id"]!.GetValue<string>());
        Assert.Equal(PageType.ToString(), body["documentType"]!["id"]!.GetValue<string>());
        Assert.Empty(body["culturesToPublish"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source)!["values"], body["values"]));
    }

    [Fact]
    public void For_CreatesADraftWithoutPublishing_AtTheRoot()
    {
        var row = Row(PlanOutcome.Create) with { ParentKey = null };

        var write = TransferRequests.For(row, Fixture("document-draft-page.json"));

        Assert.Equal(("POST", $"{Api}/document"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Null(body["parent"]);
        Assert.False(body.ContainsKey("culturesToPublish"));
    }

    [Fact]
    public void For_UpdatesAndPublishesTheChosenCultures_LeavingOutACultureNotCreatedHere()
    {
        var write = TransferRequests.For(Row(PlanOutcome.Update, "en-US"), VariantSource());

        Assert.Equal(("PUT", $"{Api}/document/{Key:D}/update-and-publish"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Equal(["en-US"], body["culturesToPublish"]!.AsArray().Select(culture => culture!.GetValue<string>()));
        Assert.Equal(["Page en"], body["variants"]!.AsArray().Select(variant => variant!["name"]!.GetValue<string>()));
        Assert.Equal(["title", "shared"], body["values"]!.AsArray().Select(value => value!["alias"]!.GetValue<string>()));
        Assert.Equal(Template.ToString(), body["template"]!["id"]!.GetValue<string>());
        Assert.False(body.ContainsKey("id"));
        Assert.False(body.ContainsKey("parent"));
        Assert.False(body.ContainsKey("documentType"));
    }

    [Fact]
    public void For_SavesAnOverwrittenConflictWithoutPublishing()
    {
        var write = TransferRequests.For(Row(PlanOutcome.Conflict), VariantSource());

        Assert.Equal(("PUT", $"{Api}/document/{Key:D}"), (write.Method, write.Path));
        Assert.False(Body(write).ContainsKey("culturesToPublish"));
    }

    [Theory]
    [InlineData(PlanOutcome.Unchanged)]
    [InlineData(PlanOutcome.Blocked)]
    public void For_RefusesARowThatWritesNothing(PlanOutcome outcome)
    {
        Assert.Throws<ArgumentException>(() => TransferRequests.For(Row(outcome), VariantSource()));
    }

    [Fact]
    public void For_RefusesAMediaRow()
    {
        var media = Row(PlanOutcome.Create) with { Kind = PlanItemKind.Media };

        Assert.Throws<ArgumentException>(() => TransferRequests.For(media, VariantSource()));
    }

    [Fact]
    public void Sort_SendsTheWholeOrderUnderTheParent_OrTheRoot()
    {
        var other = Guid.NewGuid();

        var write = TransferRequests.Sort(Parent, [other, Key]);

        Assert.Equal(("PUT", $"{Api}/document/sort"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Equal(Parent.ToString(), body["parent"]!["id"]!.GetValue<string>());
        Assert.Equal(
            [(other.ToString(), 0), (Key.ToString(), 1)],
            body["sorting"]!.AsArray().Select(item => (item!["id"]!.GetValue<string>(), item["sortOrder"]!.GetValue<int>())));
        Assert.Null(Body(TransferRequests.Sort(null, [Key]))["parent"]);
    }

    private static readonly Guid TheirTemplate = new("88888888-8888-4888-8888-888888888888");

    private static PlanRow FieldRow(PlanOutcome outcome, FieldChoice[] fields, params string?[] publish) => Row(outcome, publish) with { Fields = fields };

    private static FieldChoice ValueOf(string alias, string? culture = null) => new(FieldKind.Value, alias, culture, null);

    // A document with values (alias, culture, value) and variants (culture, name, state).
    private static string Document(Guid template, (string Alias, string? Culture, string Value)[] values, params (string? Culture, string Name, string State)[] variants)
        => new JsonObject
        {
            ["template"] = new JsonObject { ["id"] = template.ToString() },
            ["documentType"] = new JsonObject { ["id"] = PageType.ToString() },
            ["values"] = new JsonArray([.. values.Select(value => (JsonNode)new JsonObject
            {
                ["editorAlias"] = "Umbraco.TextBox",
                ["alias"] = value.Alias,
                ["culture"] = value.Culture,
                ["segment"] = null,
                ["value"] = value.Value,
            })]),
            ["variants"] = new JsonArray([.. variants.Select(variant => (JsonNode)new JsonObject
            {
                ["culture"] = variant.Culture,
                ["segment"] = null,
                ["name"] = variant.Name,
                ["state"] = variant.State,
            })]),
        }.ToJsonString();

    private static Dictionary<string, string?> Values(JsonObject body)
        => body["values"]!.AsArray().ToDictionary(
            value => $"{value!["alias"]!.GetValue<string>()}/{value["culture"]?.GetValue<string>()}",
            value => value!["value"]?.GetValue<string>());

    [Fact]
    public void ForFields_TakesOnlyTheChosenValueFromHere_AndKeepsEverythingElseOfTheTarget()
    {
        var source = Document(Template, [("title", null, "Ours"), ("intro", null, "Our intro")], (null, "Our name", "Published"));
        var target = Document(TheirTemplate, [("title", null, "Theirs"), ("intro", null, "Their intro"), ("extra", null, "Only there")], (null, "Their name", "Published"));

        var write = TransferRequests.ForFields(FieldRow(PlanOutcome.Update, [ValueOf("title")]), source, target);

        Assert.Equal(("PUT", $"{Api}/document/{Key:D}"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Equal(new Dictionary<string, string?> { ["title/"] = "Ours", ["intro/"] = "Their intro", ["extra/"] = "Only there" }, Values(body));
        Assert.Equal(["Their name"], body["variants"]!.AsArray().Select(variant => variant!["name"]!.GetValue<string>()));
        Assert.Equal(TheirTemplate.ToString(), body["template"]!["id"]!.GetValue<string>());
        Assert.False(body.ContainsKey("culturesToPublish"));
    }

    [Fact]
    public void ForFields_ClearsAChosenValueThatIsEmptyHere()
    {
        var source = Document(Template, [("title", null, "Ours")], (null, "Page", "Published"));
        var target = Document(TheirTemplate, [("title", null, "Theirs"), ("intro", null, "Their intro")], (null, "Page", "Published"));

        var write = TransferRequests.ForFields(FieldRow(PlanOutcome.Update, [ValueOf("intro")]), source, target);

        Assert.Equal(new Dictionary<string, string?> { ["title/"] = "Theirs" }, Values(Body(write)));
    }

    [Fact]
    public void ForFields_TakesAChosenNameInItsOwnCultureOnly_AndPublishesThePlannedCultures()
    {
        var source = Document(Template, [("title", "en-US", "en")], ("en-US", "New en", "Published"), ("nl-BE", "New nl", "Published"));
        var target = Document(TheirTemplate, [("title", "en-US", "en")], ("en-US", "Old en", "Published"), ("nl-BE", "Old nl", "Published"));

        var write = TransferRequests.ForFields(FieldRow(PlanOutcome.Update, [new FieldChoice(FieldKind.Name, null, "EN-us", null)], "en-US"), source, target);

        Assert.Equal(("PUT", $"{Api}/document/{Key:D}/update-and-publish"), (write.Method, write.Path));
        var body = Body(write);
        Assert.Equal(["New en", "Old nl"], body["variants"]!.AsArray().Select(variant => variant!["name"]!.GetValue<string>()));
        Assert.Equal(["en-US"], body["culturesToPublish"]!.AsArray().Select(culture => culture!.GetValue<string>()));
    }

    [Fact]
    public void ForFields_CreatesACultureTheTargetLacks_OnlyForAChosenField()
    {
        var source = Document(Template, [("title", "en-US", "en"), ("title", "de-DE", "de"), ("title", "nl-BE", "nl")],
            ("en-US", "Page en", "Published"), ("de-DE", "Page de", "Draft"), ("nl-BE", "Page nl", "Draft"));
        var target = Document(TheirTemplate, [("title", "en-US", "en")], ("en-US", "Page en", "Published"), ("de-DE", "Seite", "NotCreated"));

        var write = TransferRequests.ForFields(FieldRow(PlanOutcome.Update, [ValueOf("title", "de-DE")]), source, target);

        var body = Body(write);
        Assert.Equal(["en-US", "de-DE"], body["variants"]!.AsArray().Select(variant => variant!["culture"]!.GetValue<string>()));
        Assert.Equal("Page de", body["variants"]![1]!["name"]!.GetValue<string>());
        Assert.Equal(new Dictionary<string, string?> { ["title/en-US"] = "en", ["title/de-DE"] = "de" }, Values(body));
    }

    [Fact]
    public void ForAndForFields_NeverWriteAFieldRowAsAWholeNode_NorTheOtherWayRound()
    {
        var json = Document(Template, [("title", null, "x")], (null, "Page", "Published"));

        Assert.Throws<ArgumentException>(() => TransferRequests.For(FieldRow(PlanOutcome.Update, [ValueOf("title")]), json));
        Assert.Throws<ArgumentException>(() => TransferRequests.ForFields(Row(PlanOutcome.Update), json, json));
        Assert.Throws<ArgumentException>(() => TransferRequests.ForFields(FieldRow(PlanOutcome.Create, [ValueOf("title")]), json, json));
        Assert.Throws<ArgumentException>(() => TransferRequests.ForFields(FieldRow(PlanOutcome.Unchanged, [ValueOf("title")]), json, json));
    }
}