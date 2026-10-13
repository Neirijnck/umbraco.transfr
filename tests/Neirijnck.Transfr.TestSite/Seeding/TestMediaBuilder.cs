using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.TemporaryFile;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Creates media folders and images the way the backoffice does: upload a temporary file, then create the media item
/// with <c>umbracoFile = { temporaryFileId }</c>.
/// </summary>
public sealed class TestMediaBuilder
{
    private readonly ITemporaryFileService temporaryFileService;
    private readonly IMediaEditingService mediaEditingService;

    public TestMediaBuilder(ITemporaryFileService temporaryFileService, IMediaEditingService mediaEditingService)
    {
        this.temporaryFileService = temporaryFileService;
        this.mediaEditingService = mediaEditingService;
    }

    public Task<Guid> CreateFolderAsync(string name, Guid? parentKey)
        => this.CreateAsync(UmbracoConstants.MediaTypes.Guids.FolderGuid, name, parentKey, []);

    public async Task<Guid> CreateImageAsync(string name, Guid? parentKey, string fileName, byte[] bytes)
    {
        var temporaryFileKey = Guid.NewGuid();
        var upload = await this.temporaryFileService.CreateAsync(new CreateTemporaryFileModel
        {
            Key = temporaryFileKey,
            FileName = fileName,
            // Called twice (validation, then copy), so every call needs a fresh stream.
            OpenReadStream = () => new MemoryStream(bytes),
        });
        if (!upload.Success)
        {
            throw new InvalidOperationException($"Uploading '{fileName}' failed: {upload.Status}");
        }

        var file = new JsonObject { ["temporaryFileId"] = temporaryFileKey.ToString() };
        return await this.CreateAsync(
            UmbracoConstants.MediaTypes.Guids.ImageGuid,
            name,
            parentKey,
            [new PropertyValueModel { Alias = UmbracoConstants.Conventions.Media.File, Value = file }]);
    }

    private async Task<Guid> CreateAsync(Guid mediaTypeKey, string name, Guid? parentKey, IEnumerable<PropertyValueModel> properties)
    {
        var result = await this.mediaEditingService.CreateAsync(
            new MediaCreateModel
            {
                Key = Guid.NewGuid(),
                ContentTypeKey = mediaTypeKey,
                ParentKey = parentKey,
                Variants = [new VariantModel { Name = name }],
                Properties = properties,
            },
            UmbracoConstants.Security.SuperUserKey);

        // CreateAsync reports Success with Status PropertyValidationError and still saves: check the status itself.
        return result.Status == ContentEditingOperationStatus.Success && result.Result.Content is not null
            ? result.Result.Content.Key
            : throw new InvalidOperationException($"Creating media '{name}' failed: {result.Status}");
    }
}