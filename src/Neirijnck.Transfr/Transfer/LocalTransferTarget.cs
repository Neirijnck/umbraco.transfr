using System.Security.Claims;
using System.Text.Json;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;
using Umbraco.Cms.Api.Management.ViewModels.Document;
using Umbraco.Cms.Api.Management.ViewModels.Media;
using Umbraco.Cms.Api.Management.ViewModels.Sorting;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.TemporaryFile;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// This environment as the side a transfer writes to. Carries out the same writes a copy to a remote sends, the way
/// Umbraco's own Management API controllers do (measured to give the remote's result exactly): the signed-in user's
/// permission on the item first, then the request model mapped by the presentation factory and saved through the editing
/// service, as that user.
/// </summary>
internal sealed class LocalTransferTarget : ITransferTarget
{
    private readonly LocalTransferServices services;
    private readonly ClaimsPrincipal user;
    private readonly Guid userKey;

    public LocalTransferTarget(LocalTransferServices services, ClaimsPrincipal user, Guid userKey)
    {
        this.services = services;
        this.user = user;
        this.userKey = userKey;
    }

    public int Requests => 0;

    public string Name => "here";

    public async Task<string?> SendAsync(DocumentWrite write, CancellationToken cancellationToken)
    {
        if (WriteRoute.Parse(write.Method, write.Path) is not { } route)
        {
            return $"Not a write a transfer makes: {write.Method} {write.Path}.";
        }

        var s = this.services;
        try
        {
            switch (route.Operation)
            {
                case WriteOperation.CreateDocument:
                {
                    var request = this.Read<CreateDocumentRequestModel>(write.Body);
                    if (!await this.MayAsync(ContentPermissionResource.WithKeys(ActionNew.ActionLetter, request.Parent?.Id)))
                    {
                        return Denied("create a document under its parent");
                    }

                    var attempt = await s.ContentEditing.CreateAsync(s.Documents.MapCreateModel(request), this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                case WriteOperation.CreateAndPublishDocument:
                {
                    var request = this.Read<CreateAndPublishDocumentRequestModel>(write.Body);
                    if (!await this.MayAsync(ContentPermissionResource.WithKeys(ActionNew.ActionLetter, request.Parent?.Id))
                        || !await this.MayAsync(ContentPermissionResource.WithKeys(ActionPublish.ActionLetter, request.Parent?.Id, request.CulturesToPublish)))
                    {
                        return Denied("create and publish a document under its parent");
                    }

                    var attempt = await s.ContentEditing.CreateAndPublishAsync(s.Documents.MapCreateModel(request), request.CulturesToPublish, this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                case WriteOperation.UpdateDocument:
                {
                    var key = route.Key!.Value;
                    if (!await this.MayAsync(ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, key)))
                    {
                        return Denied("update this document");
                    }

                    var attempt = await s.ContentEditing.UpdateAsync(key, s.Documents.MapUpdateModel(this.Read<UpdateDocumentRequestModel>(write.Body)), this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                case WriteOperation.UpdateAndPublishDocument:
                {
                    var key = route.Key!.Value;
                    var request = this.Read<UpdateAndPublishDocumentRequestModel>(write.Body);
                    if (!await this.MayAsync(ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, key))
                        || !await this.MayAsync(ContentPermissionResource.WithKeys(ActionPublish.ActionLetter, key, request.CulturesToPublish)))
                    {
                        return Denied("update and publish this document");
                    }

                    var attempt = await s.ContentEditing.UpdateAndPublishAsync(key, s.Documents.MapUpdateModel(request), request.CulturesToPublish, this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                case WriteOperation.SortDocuments:
                {
                    var request = this.Read<SortingRequestModel>(write.Body);
                    var keys = request.Sorting.Select(item => (Guid?)item.Id).Append(request.Parent?.Id);
                    if (!await this.MayAsync(ContentPermissionResource.WithKeys(ActionSort.ActionLetter, keys)))
                    {
                        return Denied("sort these documents");
                    }

                    var status = await s.ContentEditing.SortAsync(
                        request.Parent?.Id,
                        request.Sorting.Select(item => new SortingModel { Key = item.Id, SortOrder = item.SortOrder }),
                        this.userKey);
                    return Refused(status, null);
                }

                case WriteOperation.CreateMedia:
                {
                    var request = this.Read<CreateMediaRequestModel>(write.Body);
                    if (!await this.MayAsync(MediaPermissionResource.WithKeys(request.Parent?.Id)))
                    {
                        return Denied("create a media item in its folder");
                    }

                    var attempt = await s.MediaEditing.CreateAsync(s.Media.MapCreateModel(request), this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                case WriteOperation.UpdateMedia:
                {
                    var key = route.Key!.Value;
                    if (!await this.MayAsync(MediaPermissionResource.WithKeys(key)))
                    {
                        return Denied("update this media item");
                    }

                    var attempt = await s.MediaEditing.UpdateAsync(key, s.Media.MapUpdateModel(this.Read<UpdateMediaRequestModel>(write.Body)), this.userKey);
                    return Refused(attempt.Status, attempt.Result?.ValidationResult);
                }

                default:
                    return $"Not a write a transfer makes: {write.Method} {write.Path}.";
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return exception.Message;
        }
    }

    public async Task<string?> UploadAsync(MediaUpload upload, Stream file, CancellationToken cancellationToken)
    {
        // Umbraco may open the temporary file more than once while it saves the media item.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var attempt = await this.services.TemporaryFiles.CreateAsync(new CreateTemporaryFileModel
        {
            Key = upload.TemporaryFileId,
            FileName = upload.FileName,
            OpenReadStream = () => new MemoryStream(bytes, writable: false),
        });
        return attempt.Success ? null : $"Keeping {upload.FileName} as a temporary file here failed: {attempt.Status}.";
    }

    public Task<IReadOnlyList<Guid>> ReadOrderAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Guid>>([.. this.services.Tree.ReadLevel(parentKey, culture).Select(child => child.Key)]);

    public Task<string?> ReadAsync(PlanItemKind kind, Guid key, CancellationToken cancellationToken)
        => kind == PlanItemKind.Media ? this.services.MediaReader.ReadAsync(key) : this.services.DocumentReader.ReadAsync(key);

    // Umbraco can answer a save with success and still a failing status (a property that doesn't validate, saved anyway,
    // measured): only Success is written as asked.
    private static string? Refused(ContentEditingOperationStatus status, ContentValidationResult? validation)
    {
        if (status == ContentEditingOperationStatus.Success)
        {
            return null;
        }

        var properties = validation?.ValidationErrors.Select(error => error.Alias).Distinct().ToList() ?? [];
        return properties.Count == 0
            ? $"Umbraco refused it here: {status}."
            : $"Umbraco refused it here: {status} ({string.Join(", ", properties)}).";
    }

    private static string Denied(string what) => $"You may not {what} here: your user groups lack the permission.";

    private async Task<bool> MayAsync(IPermissionResource resource)
    {
        var policy = resource is MediaPermissionResource ? AuthorizationPolicies.MediaPermissionByResource : AuthorizationPolicies.ContentPermissionByResource;
        return (await this.services.Authorization.AuthorizeResourceAsync(this.user, resource, policy)).Succeeded;
    }

    private T Read<T>(string body)
        => JsonSerializer.Deserialize<T>(body, this.services.JsonOptions) ?? throw new JsonException($"The write's body can't be read as {typeof(T).Name}.");
}