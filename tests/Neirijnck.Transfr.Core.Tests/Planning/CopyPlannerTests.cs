using System.Globalization;
using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.History;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Tests.Planning;

/// <summary>
/// Documents and media items in the <c>GET document/{id}</c> and <c>GET media/{id}</c> shapes measured on an Umbraco 17.7.0
/// test site (see Fixtures/document-*.json and Fixtures/media-*.json).
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
    private static readonly Guid ImageType = new("cc07b313-0843-4aa8-bbda-871c8da728c8");
    private static readonly Guid FolderType = new("f38bd2d7-65d0-48e6-95dc-87ce06ec2d3d");
    private static readonly Guid FileType = new("4c52d8ab-54e6-40cd-999c-7a5f24903e4d");

    private static string Document(
        Guid? documentType = null,
        string? title = "Title",
        Guid? related = null,
        Guid? image = null,
        Guid? member = null,
        bool trashed = false,
        string updateDate = Updated,
        string name = "Page",
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
                ["name"] = culture is null ? name : $"{name} {culture}",
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

    private static string MediaJson(Guid? mediaType = null, string src = "/media/abcd1234/blue.png", string updateDate = Updated, string name = "Blue")
        => new JsonObject
        {
            ["isTrashed"] = false,
            ["mediaType"] = new JsonObject { ["id"] = (mediaType ?? ImageType).ToString(), ["icon"] = "icon-picture" },
            ["values"] = mediaType == FolderType
                ? new JsonArray()
                : new JsonArray(Value("umbracoFile", "Umbraco.ImageCropper", new JsonObject { ["focalPoint"] = null, ["crops"] = new JsonArray(), ["src"] = src })),
            ["variants"] = new JsonArray(new JsonObject { ["createDate"] = Updated, ["updateDate"] = updateDate, ["culture"] = null, ["segment"] = null, ["name"] = name }),
        }.ToJsonString();

    private static LocalMediaItem Image(Guid key, Guid? parentKey, string name = "Blue") => new(key, parentKey, name, MediaJson(name: name));

    private static LocalMediaItem Folder(Guid key, Guid? parentKey, string name) => new(key, parentKey, name, MediaJson(FolderType, name: name));

    private static void AssertRow(PlanRow row, PlanOutcome outcome, params PlanNote[] notes)
    {
        Assert.Equal(outcome, row.Outcome);
        Assert.Equal(notes, row.Notes);
    }

    // The target's copy of a media item, outside its recycle bin.
    private static TargetMediaItem There(Guid? parentKey, string? json = null) => new(parentKey, false, json);

    private static JsonObject Value(string alias, string editorAlias, JsonNode? value)
        => new() { ["editorAlias"] = editorAlias, ["alias"] = alias, ["culture"] = null, ["segment"] = null, ["value"] = value };

    private static SourceDocument Source(Guid key, string json, Guid? parentKey = null, string name = "Page", params FieldChoice[] fields)
        => new(key, parentKey, name, json, fields.Length == 0 ? null : fields);

    private static FieldChoice ValueOf(string alias, string? culture = null) => new(FieldKind.Value, alias, culture, null);

    private static FieldChoice NameIn(string? culture) => new(FieldKind.Name, null, culture, null);

    // The target as our last copy left it, so a difference is an update rather than a conflict.
    private static Dictionary<Guid, DateTimeOffset> CopiedAsIs(Guid key) => new() { [key] = DateTimeOffset.Parse(Updated, CultureInfo.InvariantCulture) };

    private static SchemaCheck Ok(Guid documentType) => new(documentType, []);

    private static CopyPlanFacts Facts(
        IReadOnlyList<SourceDocument> sources,
        Dictionary<Guid, TargetDocument>? targets = null,
        Guid[]? onTarget = null,
        Guid[]? published = null,
        TargetSibling[]? siblings = null,
        LocalSibling[]? localSiblings = null,
        SchemaCheck[]? schema = null,
        LocalMediaItem[]? localMedia = null,
        Dictionary<Guid, TargetMediaItem>? targetMedia = null,
        SchemaCheck[]? mediaSchema = null,
        Guid[]? pickedMedia = null,
        Dictionary<Guid, DateTimeOffset>? baseline = null)
        => new()
        {
            Sources = sources,
            TargetDocuments = targets ?? [],
            DocumentsOnTarget = (onTarget ?? [Home]).ToHashSet(),
            PublishedOnTarget = (published ?? onTarget ?? [Home]).ToHashSet(),
            TargetSiblings = siblings ?? [],
            LocalSiblings = localSiblings ?? [],
            Schema = (schema ?? [Ok(PageType), Ok(OtherType)]).ToDictionary(check => check.TypeKey),
            LocalMedia = (localMedia ?? []).ToDictionary(item => item.Key),
            TargetMedia = targetMedia ?? [],
            PickedMedia = pickedMedia ?? [],
            MediaSchema = (mediaSchema ?? [Ok(ImageType), Ok(FolderType)]).ToDictionary(check => check.TypeKey),
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

    // Pulling: the source is the remote's document, the target this environment's. A push recorded this side as its
    // source, so pulling a later remote edit back is an update while this side is as the push left it.
    [Theory]
    [InlineData(Updated, PlanOutcome.Update)]
    [InlineData("2026-10-14T11:00:00+02:00", PlanOutcome.Conflict)]
    public void Plan_PullingAfterAPush_TakesTheBaselineFromThePushSource(string hereUpdated, PlanOutcome expected)
    {
        var push = new RecordedCopy(
            CopyDirection.ToRemote,
            TargetUpdate: DateTimeOffset.Parse("2026-10-14T10:30:00+02:00", CultureInfo.InvariantCulture),
            SourceUpdate: DateTimeOffset.Parse(Updated, CultureInfo.InvariantCulture));
        var baseline = CopyBaseline.TargetUpdate(push, CopyDirection.FromRemote)!.Value;

        var row = Single(Facts(
            [Source(Page, Document(title: "Edited on the remote", updateDate: "2026-10-14T12:00:00+02:00"), Home)],
            targets: new() { [Page] = new(Home, Document(updateDate: hereUpdated)) },
            baseline: new() { [Page] = baseline }));

        Assert.Equal(expected, row.Outcome);
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
    public void Plan_SavesANodeAsADraftUnderAParentThatIsNotPublishedOnTheTarget()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], published: []));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
        Assert.Empty(row.PublishCultures);
        Assert.Equal([new PlanNote(PlanNoteKind.ParentNotPublished, Home)], row.Notes);
    }

    [Fact]
    public void Plan_PublishesUnderAParentThisPlanPublishes_NotUnderOneItCreatesAsADraft()
    {
        var published = CopyPlanner.Plan(Facts([Source(Page, Document(), Home), Source(Child, Document(), Page)]));
        Assert.Equal([null], RowOf(published, Child).PublishCultures);

        var draft = CopyPlanner.Plan(Facts([Source(Page, Document(variants: [(null, "Draft")]), Home), Source(Child, Document(), Page)]));
        Assert.Empty(RowOf(draft, Child).PublishCultures);
        Assert.Equal([new PlanNote(PlanNoteKind.ParentNotPublished, Page)], RowOf(draft, Child).Notes);
    }

    [Fact]
    public void Plan_SpreadsADraftParentDownTheChain_AndPublishesAtTheRoot()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(), null), Source(Child, Document(), Page), Source(Other, Document(), Child, "Other")],
            published: []));

        Assert.Equal([null], RowOf(rows, Page).PublishCultures);
        Assert.Equal([null], RowOf(rows, Child).PublishCultures);

        var underDraft = CopyPlanner.Plan(Facts(
            [Source(Child, Document(), Home), Source(Other, Document(), Child, "Other")],
            published: []));
        Assert.All(underDraft, row => Assert.Empty(row.PublishCultures));
        Assert.Equal([new PlanNote(PlanNoteKind.ParentNotPublished, Child)], RowOf(underDraft, Other).Notes);
    }

    [Fact]
    public void Plan_SaysNothingAboutTheParentOfANodeThatIsNotPublishedAnyway()
    {
        var row = Single(Facts([Source(Page, Document(variants: [(null, "Draft")]), Home)], published: []));

        Assert.Empty(row.Notes);
    }

    private static TargetSibling[] OnTarget(Guid? parent, params Guid[] keys) => [.. keys.Select(key => new TargetSibling(parent, key, key.ToString()))];

    private static LocalSibling[] Here(Guid? parent, params Guid[] keys) => [.. keys.Select(key => new LocalSibling(parent, key))];

    [Fact]
    public void Plan_PlacesANewNodeAfterItsPredecessorHere_NotAtTheEnd()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], siblings: OnTarget(Home, Other, Third), localSiblings: Here(Home, Other, Page, Third)));

        Assert.Equal(PlanOutcome.Create, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.PlacedAfter, Other)], row.Notes);
    }

    [Fact]
    public void Plan_SaysNothingAboutANewNodeThatIsLastHereToo()
    {
        var row = Single(Facts([Source(Page, Document(), Home)], siblings: OnTarget(Home, Other), localSiblings: Here(Home, Other, Page)));

        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_PlacesAnUnchangedNodeThatIsInAnotherPlace_AndFirstWhenItIsFirstHere()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(), Home), Source(Third, Document(), Home, "Third")],
            targets: new() { [Page] = new(Home, Document()), [Third] = new(Home, Document()) },
            onTarget: [Home, Page, Third],
            siblings: OnTarget(Home, Page, Other, Third),
            localSiblings: Here(Home, Third, Other, Page)));

        Assert.Equal([new PlanNote(PlanNoteKind.PlacedAfter, Other)], RowOf(rows, Page).Notes);
        Assert.Equal([new PlanNote(PlanNoteKind.PlacedAfter, null)], RowOf(rows, Third).Notes);
        Assert.All(rows, row => Assert.Equal(PlanOutcome.Unchanged, row.Outcome));
    }

    [Fact]
    public void Plan_DoesNotPlaceASkippedConflict_OrANodeUnderAnotherParentOnTheTarget()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(title: "New"), Home), Source(Third, Document(), Home, "Third")],
            targets: new() { [Page] = new(Home, Document(title: "Old")), [Third] = new(Other, Document()) },
            onTarget: [Home, Page, Third, Other],
            siblings: OnTarget(Home, Page, Other),
            localSiblings: Here(Home, Other, Page, Third)));

        Assert.Equal(PlanOutcome.Conflict, RowOf(rows, Page).Outcome);
        Assert.DoesNotContain(rows.SelectMany(row => row.Notes), note => note.Kind == PlanNoteKind.PlacedAfter);
    }

    [Fact]
    public void Plan_FingerprintsBothSides_SoASaveOnEitherSideChangesIt()
    {
        string FingerprintOf(string source, string? target)
            => Single(Facts([Source(Page, source, Home)], targets: target is null ? null : new() { [Page] = new(Home, target) })).Fingerprint!;

        var created = FingerprintOf(Document(), null);
        var updated = FingerprintOf(Document(), Document(title: "Old"));

        Assert.Equal($"{DateTimeOffset.Parse(Updated, System.Globalization.CultureInfo.InvariantCulture):O}|absent", created);
        Assert.NotEqual(updated, FingerprintOf(Document(updateDate: "2026-10-14T12:00:00+02:00"), Document(title: "Old")));
        Assert.NotEqual(updated, FingerprintOf(Document(), Document(title: "Old", updateDate: "2026-10-14T12:00:00+02:00")));
        Assert.Equal(updated, FingerprintOf(Document(title: "Changed but not saved again"), Document(title: "Old")));
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
            localMedia: [Folder(Images, null, "Images"), Folder(Nested, Images, "Nested"), Image(Blue, Nested)],
            targetMedia: new() { [Images] = There(null) }));

        Assert.Equal(
            [(PlanItemKind.Media, Nested), (PlanItemKind.Media, Blue), (PlanItemKind.Document, Page), (PlanItemKind.Document, Other)],
            rows.Select(row => (row.Kind, row.Key)));
        Assert.All(rows, row => Assert.Equal(PlanOutcome.Create, row.Outcome));
        Assert.Equal(Images, RowOf(rows, Nested).ParentKey);
        Assert.Equal(ImageType, RowOf(rows, Blue).DocumentTypeKey);
        Assert.Equal(FolderType, RowOf(rows, Nested).DocumentTypeKey);
        Assert.All(rows.Where(row => row.Kind == PlanItemKind.Media), row => Assert.EndsWith("|absent", row.Fingerprint, StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_PlansNoMediaForAnUnchangedOrBlockedNode()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home), Source(Other, Document(image: Blue, member: Member), Home, "Other")],
            targets: new() { [Page] = new(Home, Document(image: Blue)) },
            localMedia: [Image(Blue, null)]));

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
        var facts = Facts([Source(Page, Document(image: Blue), Home)], localMedia: [Image(Blue, Nested)]);

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

    [Fact]
    public void Plan_FieldLevel_CountsOnlyTheChosenFields()
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New", related: Home, name: "New name"), Home, fields: ValueOf("title"))],
            targets: new() { [Page] = new(Home, Document(title: "Old", related: Other)) },
            onTarget: [Home, Other],
            baseline: CopiedAsIs(Page)));

        Assert.Equal(PlanOutcome.Update, row.Outcome);
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "title")], row.Differences);
        Assert.Equal([ValueOf("title")], row.Fields);
        Assert.Equal([null], row.PublishCultures);
        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_LeavesANodeUnchangedWhenOnlyOtherFieldsDiffer()
    {
        var row = Single(Facts(
            [Source(Page, Document(related: Home), Home, fields: ValueOf("title"))],
            targets: new() { [Page] = new(Home, Document(related: Other)) },
            onTarget: [Home, Other]));

        Assert.Equal(PlanOutcome.Unchanged, row.Outcome);
        Assert.Empty(row.Differences);
        Assert.Empty(row.PublishCultures);
        Assert.Empty(row.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_IsBlockedOnlyByWhatItsOwnFieldsReference()
    {
        // Other is missing on the target: it only matters when the field linking it is carried over.
        var target = new Dictionary<Guid, TargetDocument> { [Page] = new(Home, Document(title: "Old")) };
        var json = Document(title: "New", related: Other);

        var titleOnly = Single(Facts([Source(Page, json, Home, fields: ValueOf("title"))], targets: target, baseline: CopiedAsIs(Page)));
        var related = Single(Facts([Source(Page, json, Home, fields: ValueOf("related"))], targets: target, baseline: CopiedAsIs(Page)));

        Assert.Equal(PlanOutcome.Update, titleOnly.Outcome);
        Assert.Equal(PlanOutcome.Blocked, related.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.MissingReference, Other, "related")], related.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_IsBlockedForANodeTheTargetLacks()
    {
        var row = Single(Facts([Source(Page, Document(), Home, fields: ValueOf("title"))]));

        Assert.Equal(PlanOutcome.Blocked, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.NotOnTarget)], row.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_KeepsConflictsPerNode()
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New"), Home, fields: ValueOf("title"))],
            targets: new() { [Page] = new(Home, Document(title: "Old")) }));

        Assert.Equal(PlanOutcome.Conflict, row.Outcome);
        Assert.Equal([new PlanNote(PlanNoteKind.NoBaseline)], row.Notes);
    }

    [Theory]
    [InlineData("Published", "Published", true, null)]
    [InlineData("Published", "PublishedPendingChanges", false, PlanNoteKind.TargetHasDraft)]
    [InlineData("PublishedPendingChanges", "Published", false, PlanNoteKind.DraftOnly)]
    [InlineData("PublishedPendingChanges", "PublishedPendingChanges", false, PlanNoteKind.TargetHasDraft)]
    [InlineData("Published", "Draft", false, null)]
    public void Plan_FieldLevel_PublishesOnlyWhereNoDraftGoesLive(string here, string there, bool publishes, PlanNoteKind? note)
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New", variants: [(null, here)]), Home, fields: ValueOf("title"))],
            targets: new() { [Page] = new(Home, Document(title: "Old", variants: [(null, there)])) },
            baseline: CopiedAsIs(Page)));

        Assert.Equal(PlanOutcome.Update, row.Outcome);
        Assert.Equal(publishes ? [null] : [], row.PublishCultures);
        Assert.Equal(note is { } kind ? [new PlanNote(kind)] : [], row.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_AnInvariantFieldOfVariantContentReachesEveryCulture()
    {
        var row = Single(Facts(
            [Source(Page, Document(title: "New", variants: [("en-US", "Published"), ("nl-BE", "Published")]), Home, fields: ValueOf("title"))],
            targets: new() { [Page] = new(Home, Document(title: "Old", variants: [("en-US", "Published"), ("nl-BE", "PublishedPendingChanges")])) },
            baseline: CopiedAsIs(Page)));

        Assert.Equal(["en-US"], row.PublishCultures);
        Assert.Equal([new PlanNote(PlanNoteKind.TargetHasDraft, Culture: "nl-BE")], row.Notes);
    }

    [Fact]
    public void Plan_FieldLevel_ANameReachesItsOwnCultureOnly()
    {
        var row = Single(Facts(
            [Source(Page, Document(name: "New", variants: [("en-US", "Published"), ("nl-BE", "Published")]), Home, fields: NameIn("EN-us"))],
            targets: new() { [Page] = new(Home, Document(name: "Old", variants: [("en-US", "Published"), ("nl-BE", "Published")])) },
            baseline: CopiedAsIs(Page)));

        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name, Culture: "en-US")], row.Differences);
        Assert.Equal(["en-US"], row.PublishCultures);
    }

    [Fact]
    public void Plan_FieldLevel_NeverMovesTheNode()
    {
        // Here Page comes first; on the target it is last. A whole-node copy puts it first, a field-level copy leaves it.
        var facts = (params FieldChoice[] fields) => Facts(
            [Source(Page, Document(title: "New"), Home, fields: fields)],
            targets: new() { [Page] = new(Home, Document(title: "Old")) },
            siblings: [new TargetSibling(Home, Other, "Other"), new TargetSibling(Home, Page, "Page")],
            localSiblings: [new LocalSibling(Home, Page), new LocalSibling(Home, Other)],
            baseline: CopiedAsIs(Page));

        Assert.Equal([new PlanNote(PlanNoteKind.PlacedAfter)], Single(facts()).Notes);
        Assert.Empty(Single(facts(ValueOf("title"))).Notes);
    }

    [Fact]
    public void Plan_FieldLevel_PlansOnlyTheMediaItsOwnFieldsNeed()
    {
        var facts = (params FieldChoice[] fields) => Facts(
            [Source(Page, Document(title: "New", image: Blue), Home, fields: fields)],
            targets: new() { [Page] = new(Home, Document(title: "Old")) },
            localMedia: [Image(Blue, null)],
            baseline: CopiedAsIs(Page));

        Assert.DoesNotContain(CopyPlanner.Plan(facts(ValueOf("title"))), row => row.Kind == PlanItemKind.Media);
        Assert.Contains(CopyPlanner.Plan(facts(ValueOf("image"))), row => row is { Kind: PlanItemKind.Media, Key: var key } && key == Blue);
    }

    [Fact]
    public void Plan_ComparesReferencedMediaTheTargetHas_AndLeavesItsFoldersAlone()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Folder(Images, null, "Images"), Image(Blue, Images)],
            targetMedia: new() { [Images] = There(null), [Blue] = There(Images, MediaJson()) }));

        var media = Assert.Single(rows, row => row.Kind == PlanItemKind.Media);
        Assert.Equal((Blue, PlanOutcome.Unchanged), (media.Key, media.Outcome));
        Assert.Empty(media.Notes);
        Assert.Empty(media.Differences);
        Assert.Equal($"{DateTimeOffset.Parse(Updated, CultureInfo.InvariantCulture):O}|{DateTimeOffset.Parse(Updated, CultureInfo.InvariantCulture):O}", media.Fingerprint);
    }

    [Fact]
    public void Plan_UpdatesMediaTheTargetHasAsOurLastCopyLeftIt_ElseItIsAConflict()
    {
        var facts = (Dictionary<Guid, DateTimeOffset> baseline) => Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Image(Blue, null)],
            targetMedia: new() { [Blue] = There(null, MediaJson(src: "/media/abcd1234/old-blue.png")) },
            baseline: baseline);

        var update = RowOf(CopyPlanner.Plan(facts(CopiedAsIs(Blue))), Blue);
        var noBaseline = RowOf(CopyPlanner.Plan(facts([])), Blue);
        var changed = RowOf(CopyPlanner.Plan(facts(new() { [Blue] = DateTimeOffset.Parse("2026-10-01T10:00:00+02:00", CultureInfo.InvariantCulture) })), Blue);

        AssertRow(update, PlanOutcome.Update);
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Value, "umbracoFile")], update.Differences);
        AssertRow(noBaseline, PlanOutcome.Conflict, new PlanNote(PlanNoteKind.NoBaseline));
        AssertRow(changed, PlanOutcome.Conflict, new PlanNote(PlanNoteKind.ChangedSinceLastCopy));
    }

    [Fact]
    public void Plan_BlocksMediaInTheTargetsRecycleBin_WithoutBlockingTheDocument()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Folder(Images, null, "Images"), Image(Blue, Images)],
            targetMedia: new() { [Blue] = new TargetMediaItem(null, true, null) }));

        // Its folder is not created for it either.
        Assert.Equal([Blue], rows.Where(row => row.Kind == PlanItemKind.Media).Select(row => row.Key));
        AssertRow(RowOf(rows, Blue), PlanOutcome.Blocked, new PlanNote(PlanNoteKind.TrashedOnTarget));
        Assert.Equal(PlanOutcome.Create, RowOf(rows, Page).Outcome);
    }

    [Fact]
    public void Plan_BlocksWhatWouldGoUnderAFolderInTheTargetsRecycleBin()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Folder(Images, null, "Images"), Folder(Nested, Images, "Nested"), Image(Blue, Nested)],
            targetMedia: new() { [Images] = new TargetMediaItem(null, true, null) }));

        Assert.Equal(
            [
                (Images, PlanOutcome.Blocked, new PlanNote(PlanNoteKind.TrashedOnTarget)),
                (Nested, PlanOutcome.Blocked, new PlanNote(PlanNoteKind.ParentBlocked, Images)),
                (Blue, PlanOutcome.Blocked, new PlanNote(PlanNoteKind.ParentBlocked, Nested)),
            ],
            rows.Where(row => row.Kind == PlanItemKind.Media).Select(row => (row.Key, row.Outcome, Assert.Single(row.Notes))));
    }

    [Fact]
    public void Plan_BlocksMediaOfAnotherMediaTypeOnTheTarget()
    {
        var row = RowOf(
            CopyPlanner.Plan(Facts(
                [Source(Page, Document(image: Blue), Home)],
                localMedia: [Image(Blue, null)],
                targetMedia: new() { [Blue] = There(null, MediaJson(FileType)) })),
            Blue);

        AssertRow(row, PlanOutcome.Blocked, new PlanNote(PlanNoteKind.MediaTypeDiffers, FileType));
    }

    [Fact]
    public void Plan_BlocksMediaWhoseSchemaDiffers_AndWhatAFolderOfSuchATypeWouldHold()
    {
        var problem = new SchemaProblem(SchemaItemKind.MediaType, FolderType, "Folder", false, SchemaProblemReason.Differs, ["allowed child types differ"]);
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Folder(Images, null, "Images"), Image(Blue, Images)],
            mediaSchema: [Ok(ImageType), new SchemaCheck(FolderType, [problem])]));

        AssertRow(RowOf(rows, Images), PlanOutcome.Blocked, new PlanNote(PlanNoteKind.SchemaMismatch, FolderType));
        Assert.Equal([problem], RowOf(rows, Images).SchemaProblems);
        AssertRow(RowOf(rows, Blue), PlanOutcome.Blocked, new PlanNote(PlanNoteKind.ParentBlocked, Images));
    }

    [Fact]
    public void Plan_UpdatesMediaWhereItIsOnTheTarget_AndSaysItIsNotMoved()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Folder(Images, null, "Images"), Image(Blue, Images)],
            targetMedia: new() { [Blue] = There(Nested, MediaJson(name: "Blue (B)")) },
            baseline: CopiedAsIs(Blue)));

        // The folder here is not on the target, but the item is: it is updated where it is, and no folder is created.
        var media = Assert.Single(rows, row => row.Kind == PlanItemKind.Media);
        Assert.Equal((Blue, PlanOutcome.Update), (media.Key, media.Outcome));
        Assert.Equal([new PlanNote(PlanNoteKind.UnderAnotherParentOnTarget, Nested)], media.Notes);
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name)], media.Differences);
    }

    [Fact]
    public void Plan_RefusesMediaFactsItCantDecideOn()
    {
        // A referenced item on the target without its JSON, and a media type without a schema check.
        Assert.Throws<ArgumentException>(() => CopyPlanner.Plan(Facts([Source(Page, Document(image: Blue), Home)], localMedia: [Image(Blue, null)], targetMedia: new() { [Blue] = There(null) })));
        Assert.Throws<ArgumentException>(() => CopyPlanner.Plan(Facts([Source(Page, Document(image: Blue), Home)], localMedia: [Image(Blue, null)], mediaSchema: [Ok(FolderType)])));
    }

    [Fact]
    public void Plan_PlansPickedMediaWithoutAnyDocument_AfterTheFoldersTheTargetLacks()
    {
        var rows = CopyPlanner.Plan(Facts(
            [],
            localMedia: [Folder(Images, null, "Images"), Folder(Nested, Images, "Nested"), Image(Blue, Nested)],
            targetMedia: new() { [Images] = There(null) },
            pickedMedia: [Blue]));

        Assert.Equal([(Nested, PlanOutcome.Create), (Blue, PlanOutcome.Create)], rows.Select(row => (row.Key, row.Outcome)));
    }

    [Fact]
    public void Plan_ComparesAPickedMediaItemTheTargetHas()
    {
        var same = Assert.Single(CopyPlanner.Plan(Facts([], localMedia: [Image(Blue, null)], targetMedia: new() { [Blue] = There(null, MediaJson()) }, pickedMedia: [Blue])));
        var changed = Assert.Single(CopyPlanner.Plan(Facts(
            [],
            localMedia: [Image(Blue, null)],
            targetMedia: new() { [Blue] = There(null, MediaJson(src: "/media/abcd1234/old.png")) },
            pickedMedia: [Blue],
            baseline: CopiedAsIs(Blue))));

        AssertRow(same, PlanOutcome.Unchanged);
        AssertRow(changed, PlanOutcome.Update);
    }

    [Fact]
    public void Plan_ComparesAPickedFolder_ButLeavesAFolderThatOnlyHoldsAnItemAlone()
    {
        // The target has both, its folder under another name.
        var facts = (Guid picked) => Facts(
            [],
            localMedia: [Folder(Images, null, "Images"), Image(Blue, Images)],
            targetMedia: new() { [Images] = There(null, MediaJson(FolderType, name: "Pictures")), [Blue] = There(Images, MediaJson()) },
            pickedMedia: [picked]);

        var folder = Assert.Single(CopyPlanner.Plan(facts(Images)));
        AssertRow(folder, PlanOutcome.Conflict, new PlanNote(PlanNoteKind.NoBaseline));
        Assert.Equal([new ContentDifference(ContentDifferenceKind.Name)], folder.Differences);
        Assert.Equal([Blue], CopyPlanner.Plan(facts(Blue)).Select(row => row.Key));
    }

    [Fact]
    public void Plan_PlansAMediaItemPickedAndReferencedOnce_PickedFirst()
    {
        var rows = CopyPlanner.Plan(Facts(
            [Source(Page, Document(image: Blue), Home)],
            localMedia: [Image(Blue, null), Image(Nested, null, "Nested image")],
            pickedMedia: [Nested, Blue]));

        Assert.Equal([Nested, Blue, Page], rows.Select(row => row.Key));
    }
}