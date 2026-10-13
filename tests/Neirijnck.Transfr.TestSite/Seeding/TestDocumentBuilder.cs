using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Saves documents and publishes selected cultures, checking every result.
/// </summary>
public sealed class TestDocumentBuilder
{
    private readonly IContentEditingService contentEditingService;
    private readonly IContentPublishingService contentPublishingService;

    public TestDocumentBuilder(IContentEditingService contentEditingService, IContentPublishingService contentPublishingService)
    {
        this.contentEditingService = contentEditingService;
        this.contentPublishingService = contentPublishingService;
    }

    /// <summary>
    /// Saves a document. Values are passed in the backoffice's editor format (plain strings or JSON strings), so Umbraco
    /// itself writes the stored format.
    /// </summary>
    public async Task<Guid> SaveAsync(
        Guid contentTypeKey,
        Guid? parentKey,
        IEnumerable<VariantModel> variants,
        IEnumerable<PropertyValueModel> properties)
    {
        var result = await this.contentEditingService.CreateAsync(
            new ContentCreateModel
            {
                Key = Guid.NewGuid(),
                ContentTypeKey = contentTypeKey,
                ParentKey = parentKey,
                Variants = variants,
                Properties = properties,
            },
            UmbracoConstants.Security.SuperUserKey);

        // CreateAsync reports Success with Status PropertyValidationError and still saves: check the status itself.
        return result.Status == ContentEditingOperationStatus.Success && result.Result.Content is not null
            ? result.Result.Content.Key
            : throw new InvalidOperationException($"Saving '{variants.First().Name}' failed: {result.Status}");
    }

    /// <summary>
    /// Publishes the given cultures; <c>null</c> publishes an invariant document.
    /// </summary>
    public async Task PublishAsync(Guid key, params string?[] cultures)
    {
        var result = await this.contentPublishingService.PublishAsync(
            key,
            cultures.Select(culture => new CulturePublishScheduleModel { Culture = culture }).ToList(),
            UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Publishing {key} failed: {result.Status}");
        }
    }
}