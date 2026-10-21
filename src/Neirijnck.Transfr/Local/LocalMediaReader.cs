using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// Reads one media item of this environment in-process, in exactly the shape the remote's <c>GET media/{id}</c>
/// returns: the same service, the same presentation factory and the same serializer settings as Umbraco's own endpoint
/// (<c>ByKeyMediaController</c>), so both sides can be compared as they are.
/// </summary>
public sealed class LocalMediaReader
{
    private readonly IMediaEditingService mediaEditingService;
    private readonly IMediaPresentationFactory mediaPresentationFactory;
    private readonly JsonSerializerOptions serializerOptions;

    public LocalMediaReader(
        IMediaEditingService mediaEditingService,
        IMediaPresentationFactory mediaPresentationFactory,
        IOptionsMonitor<JsonOptions> jsonOptions)
    {
        this.mediaEditingService = mediaEditingService;
        this.mediaPresentationFactory = mediaPresentationFactory;
        this.serializerOptions = jsonOptions.Get(UmbracoConstants.JsonOptionsNames.BackOffice).JsonSerializerOptions;
    }

    /// <summary>
    /// The media item's JSON, or null when it doesn't exist here.
    /// </summary>
    public async Task<string?> ReadAsync(Guid key)
    {
        var media = await this.mediaEditingService.GetAsync(key);
        return media is null ? null : JsonSerializer.Serialize(this.mediaPresentationFactory.CreateResponseModel(media), this.serializerOptions);
    }
}