using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Neirijnck.Transfr.Local;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// Umbraco's services a transfer to here writes through, the ones its Management API controllers use, and the readers
/// for the target's state. Hands out a <see cref="LocalTransferTarget"/> per transfer, for the user who runs it.
/// </summary>
public sealed class LocalTransferServices
{
    public LocalTransferServices(
        IDocumentEditingPresentationFactory documents,
        IMediaEditingPresentationFactory media,
        IContentEditingService contentEditing,
        IMediaEditingService mediaEditing,
        ITemporaryFileService temporaryFiles,
        IAuthorizationService authorization,
        LocalTreeReader tree,
        LocalDocumentReader documentReader,
        LocalMediaReader mediaReader,
        IOptionsMonitor<JsonOptions> jsonOptions)
    {
        this.Documents = documents;
        this.Media = media;
        this.ContentEditing = contentEditing;
        this.MediaEditing = mediaEditing;
        this.TemporaryFiles = temporaryFiles;
        this.Authorization = authorization;
        this.Tree = tree;
        this.DocumentReader = documentReader;
        this.MediaReader = mediaReader;

        // The write bodies are Management API request models: read them as its model binding does.
        this.JsonOptions = jsonOptions.Get(UmbracoConstants.JsonOptionsNames.BackOffice).JsonSerializerOptions;
    }

    internal IDocumentEditingPresentationFactory Documents { get; }

    internal IMediaEditingPresentationFactory Media { get; }

    internal IContentEditingService ContentEditing { get; }

    internal IMediaEditingService MediaEditing { get; }

    internal ITemporaryFileService TemporaryFiles { get; }

    internal IAuthorizationService Authorization { get; }

    internal LocalTreeReader Tree { get; }

    internal LocalDocumentReader DocumentReader { get; }

    internal LocalMediaReader MediaReader { get; }

    internal JsonSerializerOptions JsonOptions { get; }

    internal LocalTransferTarget For(ClaimsPrincipal user, Guid userKey) => new(this, user, userKey);
}