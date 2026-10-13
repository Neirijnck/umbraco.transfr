using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentTypeEditing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.ContentTypeEditing;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Creates the languages, data types, element types and document types of the test fixture.
/// Every create checks its result and throws, so a broken fixture stops the boot instead of seeding half a site.
/// </summary>
public sealed class TestSchemaBuilder
{
    private readonly ILanguageService languageService;
    private readonly IDataTypeService dataTypeService;
    private readonly PropertyEditorCollection propertyEditors;
    private readonly IConfigurationEditorJsonSerializer configurationSerializer;
    private readonly IContentTypeEditingService contentTypeEditingService;
    private readonly IContentTypeService contentTypeService;

    public TestSchemaBuilder(
        ILanguageService languageService,
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configurationSerializer,
        IContentTypeEditingService contentTypeEditingService,
        IContentTypeService contentTypeService)
    {
        this.languageService = languageService;
        this.dataTypeService = dataTypeService;
        this.propertyEditors = propertyEditors;
        this.configurationSerializer = configurationSerializer;
        this.contentTypeEditingService = contentTypeEditingService;
        this.contentTypeService = contentTypeService;
    }

    public async Task CreateLanguageAsync(string isoCode, string name)
    {
        var result = await this.languageService.CreateAsync(new Language(isoCode, name), UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Creating language '{isoCode}' failed: {result.Status}");
        }
    }

    public async Task<Guid> CreateDataTypeAsync(string editorAlias, string editorUiAlias, string name, Dictionary<string, object> configuration)
    {
        if (!this.propertyEditors.TryGet(editorAlias, out var editor))
        {
            throw new InvalidOperationException($"Property editor '{editorAlias}' not found");
        }

        var dataType = new DataType(editor, this.configurationSerializer)
        {
            Key = Guid.NewGuid(),
            Name = name,
            EditorUiAlias = editorUiAlias,
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = configuration,
        };

        var result = await this.dataTypeService.CreateAsync(dataType, UmbracoConstants.Security.SuperUserKey);
        return result.Success
            ? result.Result.Key
            : throw new InvalidOperationException($"Creating data type '{name}' failed: {result.Status}");
    }

    public async Task<IContentType> CreateTypeAsync(
        string alias,
        string name,
        string icon,
        bool isElement,
        bool variesByCulture,
        bool allowedAsRoot,
        IEnumerable<TestPropertySpec> properties)
    {
        var groupKey = Guid.NewGuid();
        var model = new ContentTypeCreateModel
        {
            Key = Guid.NewGuid(),
            Alias = alias,
            Name = name,
            Icon = icon,
            IsElement = isElement,
            VariesByCulture = variesByCulture,
            AllowedAsRoot = allowedAsRoot,
            Containers = [new ContentTypePropertyContainerModel { Key = groupKey, Name = "Content", Type = nameof(PropertyGroupType.Group) }],
            Properties = properties
                .Select((property, index) => new ContentTypePropertyTypeModel
                {
                    Key = Guid.NewGuid(),
                    ContainerKey = groupKey,
                    SortOrder = index,
                    Alias = property.Alias,
                    Name = property.Name,
                    DataTypeKey = property.DataTypeKey,
                    VariesByCulture = property.VariesByCulture,
                })
                .ToList(),
        };

        var result = await this.contentTypeEditingService.CreateAsync(model, UmbracoConstants.Security.SuperUserKey);
        return result.Success && result.Result is not null
            ? result.Result
            : throw new InvalidOperationException($"Creating type '{alias}' failed: {result.Status}");
    }

    /// <summary>
    /// Sets the allowed child types after all types exist: on create, allowed types that don't exist yet (including
    /// the type itself) are dropped silently (ContentTypeEditingServiceBase.UpdateAllowedContentTypes).
    /// </summary>
    public async Task AllowChildrenAsync(IContentType parent, params IContentType[] children)
    {
        parent.AllowedContentTypes = children.Select((child, index) => new ContentTypeSort(child.Key, index, child.Alias)).ToList();
        var result = await this.contentTypeService.UpdateAsync(parent, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Setting allowed children of '{parent.Alias}' failed: {result.Result}");
        }
    }
}