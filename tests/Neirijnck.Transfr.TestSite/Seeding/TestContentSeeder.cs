using System.Text.Json.Nodes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Seeds schema, media and content into an empty test site, so compare and copy scenarios have something to work on:
/// languages, element types, Block List and Block Grid data types, rich text with internal links and an image, pickers,
/// a culture-variant document type, nested media folders, a draft and a partly published variant document.
/// Test infrastructure only: runs when <c>TransfrTestSite:SeedOnEmptyDatabase</c> is true and the site has no content.
/// Keys are deliberately new on every seed (not stable name-based keys): two separately seeded sites must end up with
/// different keys for the same aliases, which is the situation the schema check has to refuse.
/// </summary>
public sealed partial class TestContentSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string ConfigKey = "TransfrTestSite:SeedOnEmptyDatabase";
    public const string PageTypeAlias = "transfrTestPage";
    public const string VariantPageTypeAlias = "transfrVariantPage";
    public const string SecondCulture = "nl-BE";

    private readonly IConfiguration configuration;
    private readonly IRuntimeState runtimeState;
    private readonly IContentService contentService;
    private readonly ILanguageService languageService;
    private readonly IJsonSerializer jsonSerializer;
    private readonly TestSchemaBuilder schema;
    private readonly TestMediaBuilder media;
    private readonly TestDocumentBuilder documents;
    private readonly ILogger<TestContentSeeder> logger;

    public TestContentSeeder(
        IConfiguration configuration,
        IRuntimeState runtimeState,
        IContentService contentService,
        ILanguageService languageService,
        IJsonSerializer jsonSerializer,
        TestSchemaBuilder schema,
        TestMediaBuilder media,
        TestDocumentBuilder documents,
        ILogger<TestContentSeeder> logger)
    {
        this.configuration = configuration;
        this.runtimeState = runtimeState;
        this.contentService = contentService;
        this.languageService = languageService;
        this.jsonSerializer = jsonSerializer;
        this.schema = schema;
        this.media = media;
        this.documents = documents;
        this.logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (!this.configuration.GetValue<bool>(ConfigKey) || this.runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        if (this.contentService.Count() > 0)
        {
            return;
        }

        var defaultCulture = await this.languageService.GetDefaultIsoCodeAsync();
        await this.schema.CreateLanguageAsync(SecondCulture, "Dutch (Belgium)");

        // Schema: element types before the block data types that list them, data types before the document types.
        var textBlock = await this.schema.CreateTypeAsync(
            "transfrTextBlock", "Transfr Text Block", "icon-paragraph", isElement: true, variesByCulture: false, allowedAsRoot: false,
            [new TestPropertySpec("text", "Text", UmbracoConstants.DataTypes.Guids.TextareaGuid)]);
        var imageBlock = await this.schema.CreateTypeAsync(
            "transfrImageBlock", "Transfr Image Block", "icon-picture", isElement: true, variesByCulture: false, allowedAsRoot: false,
            [new TestPropertySpec("image", "Image", UmbracoConstants.DataTypes.Guids.MediaPicker3SingleImageGuid)]);

        var blockList = await this.schema.CreateDataTypeAsync(
            "Umbraco.BlockList",
            "Umb.PropertyEditorUi.BlockList",
            "Transfr Test - Block List",
            new Dictionary<string, object>
            {
                ["blocks"] = new object[]
                {
                    new { contentElementTypeKey = textBlock.Key, label = "Text" },
                    new { contentElementTypeKey = imageBlock.Key, label = "Image" },
                },
                ["validationLimit"] = new { min = 0, max = 10 },
            });
        var blockGrid = await this.schema.CreateDataTypeAsync(
            "Umbraco.BlockGrid",
            "Umb.PropertyEditorUi.BlockGrid",
            "Transfr Test - Block Grid",
            new Dictionary<string, object>
            {
                ["gridColumns"] = 12,
                ["blocks"] = new object[]
                {
                    new { contentElementTypeKey = textBlock.Key, allowAtRoot = true, allowInAreas = true, columnSpanOptions = new[] { new { columnSpan = 12 } } },
                },
            });

        var pageType = await this.schema.CreateTypeAsync(
            PageTypeAlias, "Transfr Test Page", "icon-document", isElement: false, variesByCulture: false, allowedAsRoot: true,
            [
                new TestPropertySpec("title", "Title", UmbracoConstants.DataTypes.Guids.TextstringGuid),
                new TestPropertySpec("body", "Body", UmbracoConstants.DataTypes.Guids.RichtextEditorGuid),
                new TestPropertySpec("related", "Related", UmbracoConstants.DataTypes.Guids.ContentPickerGuid),
                new TestPropertySpec("links", "Links", UmbracoConstants.DataTypes.Guids.RelatedLinksGuid),
                new TestPropertySpec("image", "Image", UmbracoConstants.DataTypes.Guids.MediaPicker3SingleImageGuid),
                new TestPropertySpec("blocks", "Blocks", blockList),
                new TestPropertySpec("grid", "Grid", blockGrid),
            ]);
        var variantPageType = await this.schema.CreateTypeAsync(
            VariantPageTypeAlias, "Transfr Variant Page", "icon-globe", isElement: false, variesByCulture: true, allowedAsRoot: false,
            [
                new TestPropertySpec("title", "Title", UmbracoConstants.DataTypes.Guids.TextstringGuid, VariesByCulture: true),
                new TestPropertySpec("body", "Body", UmbracoConstants.DataTypes.Guids.RichtextEditorGuid, VariesByCulture: true),
            ]);
        await this.schema.AllowChildrenAsync(pageType, pageType, variantPageType);
        await this.schema.AllowChildrenAsync(variantPageType, variantPageType);

        // Media: a folder chain, so copying a referenced image has to bring its folders along.
        var images = await this.media.CreateFolderAsync("Transfr Images", null);
        var nested = await this.media.CreateFolderAsync("Nested", images);
        var red = await this.media.CreateImageAsync("Red", images, "red.png", TestPng.Solid(220, 40, 40));
        var blue = await this.media.CreateImageAsync("Blue", nested, "blue.png", TestPng.Solid(40, 80, 220));
        await this.media.CreateImageAsync("Green (unreferenced)", null, "green.png", TestPng.Solid(40, 180, 80));

        // Content.
        var home = await this.SavePageAsync(pageType, null, "Home", publish: true);
        var about = await this.SavePageAsync(pageType, home, "About", publish: true);
        var news = await this.SavePageAsync(pageType, home, "News", publish: true);
        await this.SavePageAsync(pageType, news, "First Article", publish: true);
        await this.SavePageAsync(pageType, news, "Second Article", publish: true);
        await this.SavePageAsync(pageType, home, "Draft Page", publish: false);

        var features = await this.documents.SaveAsync(
            pageType.Key,
            home,
            [new VariantModel { Name = "Features" }],
            [
                Value("title", "Features"),
                Value("body", $"<p>See <a href=\"/{{localLink:{about:D}}}\" title=\"About\" type=\"document\">about</a>.</p><p><img src=\"\" alt=\"\" data-udi=\"umb://media/{red:N}\"></p>"),
                Value("related", about.ToString()),
                Value("links", Links(about)),
                Value("image", ImagePick(red)),
                Value("blocks", this.BlockList(textBlock, imageBlock, blue)),
                Value("grid", this.BlockGrid(textBlock)),
            ]);
        await this.documents.PublishAsync(features, [null]);

        var variant = await this.documents.SaveAsync(
            variantPageType.Key,
            home,
            [new VariantModel { Culture = defaultCulture, Name = "Variant Page" }, new VariantModel { Culture = SecondCulture, Name = "Variante Pagina" }],
            [
                Value("title", "Variant Page", defaultCulture),
                Value("title", "Variante Pagina", SecondCulture),
                Value("body", "<p>English body.</p>", defaultCulture),
                Value("body", "<p>Nederlandse tekst.</p>", SecondCulture),
            ]);
        await this.documents.PublishAsync(variant, defaultCulture, SecondCulture);

        // Published in the default culture only: the second culture stays a draft.
        var variantChild = await this.documents.SaveAsync(
            variantPageType.Key,
            variant,
            [new VariantModel { Culture = defaultCulture, Name = "Variant Child" }, new VariantModel { Culture = SecondCulture, Name = "Variant Kind" }],
            [Value("title", "Variant Child", defaultCulture), Value("title", "Variant Kind", SecondCulture)]);
        await this.documents.PublishAsync(variantChild, defaultCulture);

        LogSeeded(this.logger, pageType.Key, home);
    }

    private static PropertyValueModel Value(string alias, object value, string? culture = null)
        => new() { Alias = alias, Value = value, Culture = culture };

    // Multi URL picker editor format: a document link and an external link.
    private static string Links(Guid document) => new JsonArray(
        new JsonObject { ["name"] = "About", ["target"] = null, ["type"] = "document", ["unique"] = document.ToString(), ["url"] = null, ["queryString"] = null, ["culture"] = null },
        new JsonObject { ["name"] = "Umbraco", ["target"] = "_blank", ["type"] = "external", ["unique"] = null, ["url"] = "https://umbraco.com", ["queryString"] = null, ["culture"] = null })
        .ToJsonString();

    // Media Picker 3 editor format: one pick with a fresh pick key.
    private static string ImagePick(Guid mediaKey) => new JsonArray(
        new JsonObject { ["key"] = Guid.NewGuid().ToString(), ["mediaKey"] = mediaKey.ToString(), ["mediaTypeAlias"] = "Image", ["crops"] = new JsonArray(), ["focalPoint"] = null })
        .ToJsonString();

    private async Task<Guid> SavePageAsync(IContentType pageType, Guid? parent, string name, bool publish)
    {
        var key = await this.documents.SaveAsync(pageType.Key, parent, [new VariantModel { Name = name }], [Value("title", name)]);
        if (publish)
        {
            await this.documents.PublishAsync(key, [null]);
        }

        return key;
    }

    private string BlockList(IContentType textBlock, IContentType imageBlock, Guid image)
    {
        var text = Guid.NewGuid();
        var picture = Guid.NewGuid();

        // Layout items via the initializer: the Guid constructor also writes a contentUdi the backoffice never sends.
        var value = new BlockListValue([new BlockListLayoutItem { ContentKey = text }, new BlockListLayoutItem { ContentKey = picture }])
        {
            ContentData =
            [
                new BlockItemData { Key = text, ContentTypeKey = textBlock.Key, Values = [new BlockPropertyValue { Alias = "text", Value = "First block" }] },
                new BlockItemData { Key = picture, ContentTypeKey = imageBlock.Key, Values = [new BlockPropertyValue { Alias = "image", Value = ImagePick(image) }] },
            ],
            // Without an expose entry a block is not rendered; invariant elements expose culture null.
            Expose = [new BlockItemVariation(text, null, null), new BlockItemVariation(picture, null, null)],
        };
        return this.jsonSerializer.Serialize(value);
    }

    private string BlockGrid(IContentType textBlock)
    {
        var text = Guid.NewGuid();
        var value = new BlockGridValue([new BlockGridLayoutItem { ContentKey = text, ColumnSpan = 12, RowSpan = 1 }])
        {
            ContentData = [new BlockItemData { Key = text, ContentTypeKey = textBlock.Key, Values = [new BlockPropertyValue { Alias = "text", Value = "Grid block" }] }],
            Expose = [new BlockItemVariation(text, null, null)],
        };
        return this.jsonSerializer.Serialize(value);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded test content: document type {DocumentTypeKey}, root {RootKey}")]
    private static partial void LogSeeded(ILogger logger, Guid documentTypeKey, Guid rootKey);
}