using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Tests.Planning;

/// <summary>
/// Documents in the <c>GET document/{id}</c> shape measured on an Umbraco 17.7.0 test site (see Fixtures/document-*.json).
/// </summary>
public sealed class CopyPlannerTests
{
    private const string Updated = "2026-10-14T10:00:00+02:00";

    private static readonly Guid PageType = new("e2450d61-29a3-4c3d-9f6e-1b9b0b3c0a01");
    private static readonly Guid OtherType = new("82323a51-7f0e-4c43-8d3e-4a1f2b6c0b02");
    private static readonly Guid Home = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Page = new("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Child = new("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Other = new("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Third = new("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Images = new("66666666-6666-4666-8666-666666666666");
    private static readonly Guid Nested = new("77777777-7777-4777-8777-777777777777");
    private static readonly Guid Blue = new("88888888-8888-4888-8888-888888888888");
    private static readonly Guid Member = new("99999999-9999-4999-8999-999999999999");

    private static string Document(
        Guid? documentType = null,
        string? title = "Title",
        Guid? related = null,
        Guid? image = null,
        Guid? member = null,
        bool trashed = false,
        string updateDate = Updated,
        params (string? Culture, string State)[] variants)
    {
        var values = new JsonArray(Value("title", "Umbraco.TextBox", title));
        if (related is { } relatedKey)
        {
            values.Add(Value("related", "Umbraco.ContentPicker", relatedKey.ToString()));
        }

        if (image is { } imageKey)
        {
            values.Add(Value("image", "Umbraco.MediaPicker3", new JsonArray(new JsonObject { ["key"] = Guid.Empty.ToString(), ["mediaKey"] = imageKey.ToString() })));
        }

        if (member is { } memberKey)
        {
            values.Add(Value("owner", "Umbraco.MemberPicker", memberKey.ToString()));
        }

        var variantArray = new JsonArray();
        foreach (var (culture, state) in variants.Length == 0 ? [(null, "Published")] : variants)
        {
            variantArray.Add(new JsonObject
            {
                ["state"] = state,
                ["updateDate"] = updateDate,
                ["culture"] = culture,
                ["segment"] = null,
                ["name"] = culture is null ? "Page" : $"Page {culture}",
            });
        }

        return new JsonObject
        {
            ["template"] = null,
            ["isTrashed"] = trashed,
            ["documentType"] = new JsonObject { ["id"] = (documentType ?? PageType).ToString(), ["icon"] = "icon-document" },
            ["values"] = values,
            ["variants"] = variantArray,
        }.ToJsonString();
    }

    private static JsonObject Value(string alias, string editorAlias, JsonNode? value)
        => new() { ["editorAlias"] = editorAlias, ["alias"] = alias, ["culture"] = null, ["segment"] = null, ["value"] = value };

    private static SourceDocument Source(Guid key, string json, Guid? parentKey = null, string name = "Page") => new(key, parentKey, name, json);

    private static SchemaCheck Ok(Guid documentType) => new(documentType, []);

    private static CopyPlanFacts Facts(
        IReadOnlyList<SourceDocument> sources,
        Dictionary<Guid, TargetDocument>? targets = null,
        Guid[]? onTarget = null,
        TargetSibling[]? siblings = null,
        SchemaCheck[]? schema = null,
        LocalMediaItem[]? localMedia = null,
        Guid[]? mediaOnTarget = null,
        Dictionary<Guid, DateTimeOffset>? baseline = null)
        => new()
        {
            Sources = sources,
            TargetDocuments = targets ?? [],
            DocumentsOnTarget = (onTarget ?? [Home]).ToHashSet(),
            TargetSiblings = siblings ?? [],
            Schema = (schema ?? [Ok(PageType), Ok(OtherType)]).ToDictionary(check => check.DocumentTypeKey),
            LocalMedia = (localMedia ?? []).ToDictionary(item => item.Key),
            MediaOnTarget = (mediaOnTarget ?? []).ToHashSet(),
            Baseline = baseline ?? [],
        };

    private static PlanRow Single(CopyPlanFacts facts) => Assert.Single(CopyPlanner.Plan(facts));

    private static PlanRow RowOf(IReadOnlyList<PlanRow> rows, Guid key) => Assert.Single(rows, row => row.Key == key);

    [Fact]
    public void Plan_CreatesANodeTheTargetLacks_AndPublishesItsInvariantContent()
    {
        var row = Single(Facts([Source(Page, Document(), Home)]));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
        Assert.Equal(PlanItemKind.Document, row.Kind);
        Assert.Equal(PageType, row.DocumentTypeKey);
        Assert.Equal([null], row.PublishCultures);
        Assert.Empty(row.Notes);
        Assert.Empty(row.Differences);
    }

    [Fact]
    public void Plan_LeavesAnIdenticalNodeUnchanged_AndPublishesNothing()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], targets: new() { [Page] = new(Home, Document()) }));

        Assert.Equal(PlanOutcome.Unchanged, row.Outcome);
        Assert.Empty(row.PublishCultures);
        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_CallsADifferentNodeWithoutACopyOfOursAConflict()
    {
        var row = Single(Facts([Source(Page, Document(title: "New"), Home)], targets: new() { [Page] = new(Home, Document(title: "Old")) }));

        Assert.Equal(PlanOutcome.Conflict, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.NoBaseline)], row.Notes);
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "title")], row.Differences);
        Assert.Equal([null], row.PublishCultures);
    }

    [Fact]
    public void Plan_UpdatesADifferentNodeTheTargetLeftAloneSinceOurCopy()
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New"), Home)],
            targets: new() { [Page] = new(Home, Document(title: "Old")) },
            baseline: new() { [Page] = DateTimeOffset.Parse(Updated, System.Globalization.CultureInfo.InvariantCulture) }));

        Assert.Equal(PlanOutcome.Update, row.Outcome);
        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_CallsANodeChangedOnTheTargetSinceOurCopyAConflict()
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New"), Home)],
            targets: new() { [Page] = new(Home, Document(title: "Old", updateDate: "2026-10-14T11:00:00+02:00")) },
            baseline: new() { [Page] = DateTimeOffset.Parse(Updated, System.Globalization.CultureInfo.InvariantCulture) }));

        Assert.Equal(PlanOutcome.Conflict, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.ChangedSinceLastCopy)], row.Notes);
    }

    [Fact]
    public void Plan_BlocksANodeWhoseSchemaDiffers_WithTheProblems()
    {
        var problem = new SchemaProblem(SchemaItemKind.DocumentType, PageType, "Page", false, SchemaProblemReason.MissingOnRemote, []);

        var row = Single(Facts([Source(Page, Document(), Home)], schema: [new SchemaCheck(PageType, [problem])]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.SchemaMismatch, PageType)], row.Notes);
        Assert.Equal([problem], row.SchemaProblems);
        Assert.Empty(row.PublishCultures);
    }

    [Fact]
    public void Plan_BlocksANodeWhoseParentIsNowhereOnTheTarget()
    {
        var row = Single(Facts([Source(Child, Document(), Page)]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.ParentMissingOnTarget, Page)], row.Notes);
    }

    [Fact]
    public void Plan_CreatesAChildUnderAParentThisPlanCreates()
    {
        var rows = CopyPlanner.Plan(Facts([Source(Page, Document(), Home), Source(Child, Document(), Page)]));

        Assert.Equal([PlanOutcome.Create, PlanOutcome.Create], rows.Select(row => row.Outcome));
    }

    [Fact]
    public void Plan_BlocksAChildWhoseParentInThisPlanIsBlocked()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(documentType: OtherType), Home), Source(Child, Document(), Page)],
            schema: [Ok(PageType), new SchemaCheck(OtherType, [new SchemaProblem(SchemaItemKind.DocumentType, OtherType, null, false, SchemaProblemReason.Differs,["alias"])])]));

        Assert.Equal(PlanOutcome.Blocked, RowOf(rows, Page).Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.ParentBlocked, Page)], RowOf(rows, Child).Notes);
    }

    [Fact]
    public void Plan_BlocksAReferenceToADocumentTheTargetLacks_NamingTheProperty()
    {
        var row = Single(Facts([Source(Page, Document(related: Other), Home)]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.MissingReference, Other, "related")], row.Notes);
    }

    [Fact]
    public void Plan_AcceptsAReferenceToADocumentOnTheTargetOrCreatedByThisPlan()
    {
        Assert.Equal(PlanOutcome.Create, Single(Facts([Source(Page, Document(related: Other), Home)], onTarget: [Home, Other])).Outcome);

        var rows = CopyPlanner.Plan(Facts([Source(Page, Document(related: Other), Home), Source(Other, Document(), Home, "Other")]));
        Assert.All(rows, row => Assert.Equal(PlanOutcome.Create, row.Outcome));
    }

    [Fact]
    public void Plan_SpreadsABlockAlongAChainOfReferences_WhateverTheOrder()
    {
        var blockedType = new SchemaCheck(OtherType, [new SchemaProblem(SchemaItemKind.DocumentType, OtherType, null, false, SchemaProblemReason.MissingOnRemote, [])]);

        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(related: Other), Home), Source(Other, Document(related: Third), Home, "Other"), Source(Third, Document(documentType: OtherType), Home, "Third")],
            schema: [Ok(PageType), blockedType]));

        Assert.All(rows, row => Assert.Equal(PlanOutcome.Blocked, row.Outcome));
        Assert.Equal([new PlanNote(PlanNoteKind.MissingReference, Other, "related")], RowOf(rows, Page).Notes);
        Assert.Equal([new PlanNote(PlanNoteKind.MissingReference, Third, "related")], RowOf(rows, Other).Notes);
    }

    [Fact]
    public void Plan_BlocksAMemberReference()
    {
        var row = Single(Facts([Source(Page, Document(member: Member), Home)]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.MemberReference, Member, "owner")], row.Notes);
    }

    [Fact]
    public void Plan_BlocksANewNodeWithANamesakeUnderTheSameTargetParent()
    {
        var row = Single(Facts(
            [Source(Page, Document(), Home, "Contact")],
            siblings: [new TargetSibling(Home, Other, " contact "), new TargetSibling(null, Third, "Contact")]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.SameNameOtherKey, Other)], row.Notes);
    }

    [Fact]
    public void Plan_IgnoresANamesakeUnderAnotherTargetParent()
    {
        var row = Single(Facts([Source(Page, Document(), Home, "Contact")], siblings: [new TargetSibling(null, Third, "Contact")]));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
    }

    [Fact]
    public void Plan_BlocksANodeInTheTargetsRecycleBin_EvenWhenIdentical()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], targets: new() { [Page] = new(Home, Document(trashed: true)) }));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.TrashedOnTarget)], row.Notes);
    }

    [Fact]
    public void Plan_BlocksANodeOfAnotherDocumentTypeOnTheTarget()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], targets: new() { [Page] = new(Home, Document(documentType: OtherType)) }));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.DocumentTypeDiffers, OtherType)], row.Notes);
    }

    [Fact]
    public void Plan_LeavesAnIdenticalNodeUnchanged_EvenWithAReferenceTheTargetLacks()
    {
        var row = Single(Facts([Source(Page, Document(related: Other), Home)], targets: new() { [Page] = new(Home, Document(related: Other)) }));

        Assert.Equal(PlanOutcome.Unchanged, row.Outcome);
    }

    [Fact]
    public void Plan_PublishesPublishedCulturesOnly_AndSaysWhatItLeavesAlone()
    {
        var source = Document(variants: [("en-US", "Published"), ("nl-BE", "PublishedPendingChanges"), ("de-DE", "Draft"), ("fr-FR", "Draft")]);
        var target = Document(title: "Old", variants: [("en-US", "Published"), ("nl-BE", "Published"), ("de-DE", "Published"), ("fr-FR", "Draft")]);

        var row = Single(Facts([Source(Page, source, Home)], targets: new() { [Page] = new(Home, target) }));

        Assert.Equal(["en-US"], row.PublishCultures);
        Assert.Equal(
            [new PlanNote(PlanNoteKind.NoBaseline), new PlanNote(PlanNoteKind.DraftOnly, Culture: "nl-BE"), new PlanNote(PlanNoteKind.PublishStateNotApplied, Culture: "de-DE")],
            row.Notes);
    }

    [Fact]
    public void Plan_SavesADraftOnlyNodeWithoutPublishing()
    {
        var row = Single(Facts([Source(Page, Document(variants: [(null, "Draft")]), Home)]));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
        Assert.Empty(row.PublishCultures);
        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_UpdatesANodeWhereItIsOnTheTarget_AndSaysItIsNotMoved()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], targets: new() { [Page] = new(Other, Document()) }));

        Assert.Equal(PlanOutcome.Unchanged, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.UnderAnotherParentOnTarget, Other)], row.Notes);
    }

    [Fact]
    public void Plan_PlansMissingMediaAlong_FoldersFirst_OncePerItem()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home), Source(Other, Document(image: Blue), Home, "Other")],
            localMedia: [new LocalMediaItem(Images, null, "Images"), new LocalMediaItem(Nested, Images, "Nested"), new LocalMediaItem(Blue, Nested, "Blue")],
            mediaOnTarget: [Images]));

        Assert.Equal(
            [(PlanItemKind.Media, Nested), (PlanItemKind.Media, Blue), (PlanItemKind.Document, Page), (PlanItemKind.Document, Other)],
            rows.Select(row => (row.Kind, row.Key)));
        Assert.All(rows, row => Assert.Equal(PlanOutcome.Create, row.Outcome));
        Assert.Equal(Images, RowOf(rows, Nested).ParentKey);
        Assert.Null(RowOf(rows, Blue).DocumentTypeKey);
    }

    [Fact]
    public void Plan_PlansNoMediaForAnUnchangedOrBlockedNode()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home), Source(Other, Document(image: Blue, member: Member), Home, "Other")],
            targets: new() { [Page] = new(Home, Document(image: Blue)) },
            localMedia: [new LocalMediaItem(Blue, null, "Blue")]));

        Assert.DoesNotContain(rows, row => row.Kind == PlanItemKind.Media);
    }

    [Fact]
    public void Plan_NotesMediaThatIsMissingHereToo()
    {
        var row = Single(Facts([Source(Page, Document(image: Blue), Home)]));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.MediaMissingHere, Blue, "image")], row.Notes);
    }

    [Fact]
    public void Plan_RefusesAMediaChainWithAMissingFolder()
    {
        var facts = Facts([Source(Page, Document(image: Blue), Home)], localMedia: [new LocalMediaItem(Blue, Nested, "Blue")]);

        Assert.Throws<ArgumentException>(() => CopyPlanner.Plan(facts));
    }

    [Fact]
    public void Plan_RefusesASourceListedTwiceOrWithoutASchemaCheck()
    {
        Assert.Throws<ArgumentException>(() => CopyPlanner.Plan(Facts([Source(Page, Document(), Home), Source(Page, Document(), Home)])));
        Assert.Throws<ArgumentException>(() => CopyPlanner.Plan(Facts([Source(Page, Document(), Home)], schema: [Ok(OtherType)])));
    }

    [Fact]
    public void Plan_BlocksTheFeaturesPageOnEveryPropertyThatLinksAMissingPage()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "document-features.json"));
        var documentType = Guid.Parse(JsonNode.Parse(json)!["documentType"]!["id"]!.GetValue<string>());
        var about = new Guid("924c1efa-9b3d-4575-9106-166e6ed39ad1");

        var row = Single(Facts([Source(Page, json, Home)], schema: [Ok(documentType)]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal(["body", "related", "links"], row.Notes.Select(note => note.PropertyAlias));
        Assert.All(row.Notes, note => Assert.Equal((PlanNoteKind.MissingReference, about), (note.Kind, note.Key!.Value)));
    }
}