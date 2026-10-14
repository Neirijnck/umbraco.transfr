using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core.Services.Querying;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// Reads one document of this environment in-process, in exactly the shape the remote's <c>GET document/{id}</c>
/// returns: the same query, the same presentation factory and the same serializer settings as Umbraco's own endpoint
/// (<c>ByKeyDocumentController</c>), so both sides can be compared as they are.
/// </summary>
public sealed class LocalDocumentReader
{
    private readonly IContentQueryService contentQueryService;
    private readonly IDocumentPresentationFactory documentPresentationFactory;
    private readonly JsonSerializerOptions serializerOptions;

    public LocalDocumentReader(
        IContentQueryService contentQueryService,
        IDocumentPresentationFactory documentPresentationFactory,
        IOptionsMonitor<JsonOptions> jsonOptions)
    {
        this.contentQueryService = contentQueryService;
        this.documentPresentationFactory = documentPresentationFactory;
        this.serializerOptions = jsonOptions.Get(UmbracoConstants.JsonOptionsNames.BackOffice).JsonSerializerOptions;
    }

    /// <summary>
    /// The document's JSON, or null when it doesn't exist here.
    /// </summary>
    public async Task<string?> ReadAsync(Guid key)
    {
        var query = await this.contentQueryService.GetWithSchedulesAsync(key);
        if (!query.Success || query.Result?.Content is null)
        {
            return null;
        }

        var model = await this.documentPresentationFactory.CreateResponseModelAsync(query.Result.Content, query.Result.Schedules);
        return JsonSerializer.Serialize(model, this.serializerOptions);
    }
}