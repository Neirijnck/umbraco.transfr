using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Neirijnck.Transfr.Core.Schema;
using Umbraco.Cms.Api.Management.ViewModels.DataType;
using Umbraco.Cms.Api.Management.ViewModels.DocumentType;
using Umbraco.Cms.Api.Management.ViewModels.MediaType;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// Reads one schema item of this environment in-process, in exactly the shape the remote's <c>document-type/{id}</c>,
/// <c>media-type/{id}</c> and <c>data-type/{id}</c> return: the same services, the same mapping and the same serializer
/// settings as Umbraco's own endpoints (<c>ByKeyDocumentTypeController</c>, <c>ByKeyMediaTypeController</c>,
/// <c>ByKeyDataTypeController</c>).
/// </summary>
public sealed class LocalSchemaReader
{
    private readonly IContentTypeService contentTypeService;
    private readonly IMediaTypeService mediaTypeService;
    private readonly IDataTypeService dataTypeService;
    private readonly IUmbracoMapper mapper;
    private readonly JsonSerializerOptions serializerOptions;

    public LocalSchemaReader(
        IContentTypeService contentTypeService,
        IMediaTypeService mediaTypeService,
        IDataTypeService dataTypeService,
        IUmbracoMapper mapper,
        IOptionsMonitor<JsonOptions> jsonOptions)
    {
        this.contentTypeService = contentTypeService;
        this.mediaTypeService = mediaTypeService;
        this.dataTypeService = dataTypeService;
        this.mapper = mapper;
        this.serializerOptions = jsonOptions.Get(UmbracoConstants.JsonOptionsNames.BackOffice).JsonSerializerOptions;
    }

    /// <summary>The key of the document type with this alias here, or null when there is none.</summary>
    public Guid? DocumentTypeKeyByAlias(string alias) => this.contentTypeService.Get(alias)?.Key;

    /// <summary>
    /// The item's JSON, or null when it doesn't exist here.
    /// </summary>
    public async Task<string?> ReadAsync(SchemaItemKind kind, Guid key)
    {
        object? model = kind switch
        {
            SchemaItemKind.DocumentType => await this.contentTypeService.GetAsync(key) is { } type
                ? this.mapper.Map<DocumentTypeResponseModel>(type)
                : null,
            SchemaItemKind.MediaType => await this.mediaTypeService.GetAsync(key) is { } mediaType
                ? this.mapper.Map<MediaTypeResponseModel>(mediaType)
                : null,
            SchemaItemKind.DataType => await this.dataTypeService.GetAsync(key) is { } dataType
                ? this.mapper.Map<DataTypeResponseModel>(dataType)
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        return model is null ? null : JsonSerializer.Serialize(model, model.GetType(), this.serializerOptions);
    }
}