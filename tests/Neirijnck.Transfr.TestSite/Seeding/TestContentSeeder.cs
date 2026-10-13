using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentTypeEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.ContentTypeEditing;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Seeds schema and content into an empty test site, so compare scenarios have something to compare.
/// Test infrastructure only: runs when <c>TransfrTestSite:SeedOnEmptyDatabase</c> is true and the site has no content.
/// Keys are deliberately new on every seed (not stable name-based keys): two separately seeded sites must end up with
/// different keys for the same aliases, which is the situation the schema check has to refuse.
/// </summary>
public sealed partial class TestContentSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string ConfigKey = "TransfrTestSite:SeedOnEmptyDatabase";
    public const string PageTypeAlias = "transfrTestPage";

    private readonly IConfiguration configuration;
    private readonly IRuntimeState runtimeState;
    private readonly IContentService contentService;
    private readonly IContentTypeEditingService contentTypeEditingService;
    private readonly IContentTypeService contentTypeService;
    private readonly IContentEditingService contentEditingService;
    private readonly ILogger<TestContentSeeder> logger;

    public TestContentSeeder(
        IConfiguration configuration,
        IRuntimeState runtimeState,
        IContentService contentService,
        IContentTypeEditingService contentTypeEditingService,
        IContentTypeService contentTypeService,
        IContentEditingService contentEditingService,
        ILogger<TestContentSeeder> logger)
    {
        this.configuration = configuration;
        this.runtimeState = runtimeState;
        this.contentService = contentService;
        this.contentTypeEditingService = contentTypeEditingService;
        this.contentTypeService = contentTypeService;
        this.contentEditingService = contentEditingService;
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

        var pageType = await this.CreatePageTypeAsync();

        var home = await this.CreatePageAsync(pageType, null, "Home");
        await this.CreatePageAsync(pageType, home, "About");
        var news = await this.CreatePageAsync(pageType, home, "News");
        await this.CreatePageAsync(pageType, news, "First Article");
        await this.CreatePageAsync(pageType, news, "Second Article");

        LogSeeded(this.logger, pageType.Key, home);
    }

    private async Task<IContentType> CreatePageTypeAsync()
    {
        var typeKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();

        var model = new ContentTypeCreateModel
        {
            Key = typeKey,
            Alias = PageTypeAlias,
            Name = "Transfr Test Page",
            Icon = "icon-document",
            AllowedAsRoot = true,
            Containers = [new ContentTypePropertyContainerModel { Key = groupKey, Name = "Content", Type = nameof(PropertyGroupType.Group) }],
            Properties =
            [
                new ContentTypePropertyTypeModel
                {
                    Key = Guid.NewGuid(),
                    ContainerKey = groupKey,
                    Alias = "title",
                    Name = "Title",
                    DataTypeKey = UmbracoConstants.DataTypes.Guids.TextstringGuid,
                },
            ],
        };

        var result = await this.contentTypeEditingService.CreateAsync(model, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success || result.Result is null)
        {
            throw new InvalidOperationException($"Creating document type '{PageTypeAlias}' failed: {result.Status}");
        }

        // Allowing the type below itself only works once it exists: on create, allowed types that don't exist yet
        // are dropped silently (ContentTypeEditingServiceBase.UpdateAllowedContentTypes).
        var pageType = result.Result;
        pageType.AllowedContentTypes = [new ContentTypeSort(typeKey, 0, PageTypeAlias)];
        var update = await this.contentTypeService.UpdateAsync(pageType, UmbracoConstants.Security.SuperUserKey);
        return update.Success
            ? pageType
            : throw new InvalidOperationException($"Allowing '{PageTypeAlias}' below itself failed: {update.Result}");
    }

    private async Task<Guid> CreatePageAsync(IContentType pageType, Guid? parentKey, string name)
    {
        var model = new ContentCreateModel
        {
            ContentTypeKey = pageType.Key,
            ParentKey = parentKey,
            Variants = [new VariantModel { Name = name }],
            Properties = [new PropertyValueModel { Alias = "title", Value = name }],
        };

        // ContentService.SaveAndPublish rejects "*" and, for invariant content, any culture at all:
        // an invariant document is published with an empty culture list.
        var result = await this.contentEditingService.CreateAndPublishAsync(model, [], UmbracoConstants.Security.SuperUserKey);
        return result.Success && result.Result.Content is not null
            ? result.Result.Content.Key
            : throw new InvalidOperationException($"Creating and publishing '{name}' failed: {result.Status}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded test content: document type {DocumentTypeKey}, root {RootKey}")]
    private static partial void LogSeeded(ILogger logger, Guid documentTypeKey, Guid rootKey);
}