using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Security;

namespace Neirijnck.Transfr.Controllers;

public class RemotesController : TransfrApiControllerBase
{
    private readonly RemoteRegistry registry;
    private readonly RemoteClient client;
    private readonly IBackOfficeSecurityAccessor backOfficeSecurityAccessor;

    public RemotesController(RemoteRegistry registry, RemoteClient client, IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        this.registry = registry;
        this.client = client;
        this.backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    private Guid UserKey => CurrentUserKey(this.backOfficeSecurityAccessor);

    [HttpGet("remotes")]
    [ProducesResponseType<RemotesResponseModel>(StatusCodes.Status200OK)]
    public IActionResult GetRemotes()
        => this.Ok(new RemotesResponseModel(
            this.registry.RuntimeRemotesAllowed,
            this.registry.GetAll(this.UserKey).Select(ToResponse).ToList()));

    /// <summary>
    /// Connects a remote for the current user: validates it, signs in with its client credentials, and only then keeps
    /// it in memory. Nothing is kept when the sign-in fails.
    /// </summary>
    [HttpPost("remotes")]
    [ProducesResponseType<RemoteResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Connect(ConnectRemoteRequestModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!this.registry.RuntimeRemotesAllowed)
        {
            return this.NotFound();
        }

        var remote = new RemoteDefinition(RemoteAliasFactory.FromBaseUrl(model.BaseUrl), model.Name, model.BaseUrl, model.ClientId, model.ClientSecret);
        var errors = this.registry.ValidateNew(this.UserKey, remote);
        if (errors.Count > 0)
        {
            return this.Problem(title: "The remote is not valid", detail: string.Join(" ", errors), statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await this.client.AuthenticateAsync(remote, cancellationToken);
        }
        catch (RemoteAuthenticationException exception)
        {
            return this.Problem(title: "Signing in to the remote failed", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException exception)
        {
            return this.Problem(title: "The remote could not be reached", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        this.registry.Connect(this.UserKey, remote);
        return this.Ok(ToResponse(new RegisteredRemote(new RemoteValidationResult(remote, []), RemoteSource.Runtime)));
    }

    /// <summary>
    /// Forgets a remote the current user connected at runtime. Configured remotes can't be disconnected here.
    /// </summary>
    [HttpDelete("remotes/{alias}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Disconnect(string alias)
        => this.registry.Disconnect(this.UserKey, alias) ? this.Ok() : this.NotFound();

    /// <summary>
    /// Tests the connection: is the remote reachable, do the client credentials work, can the API user read content.
    /// </summary>
    [HttpGet("remotes/{alias}/status")]
    [ProducesResponseType<RemoteStatusResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string alias, CancellationToken cancellationToken)
    {
        var remote = this.registry.FindValid(this.UserKey, alias);
        if (remote is null)
        {
            return this.NotFound();
        }

        string? serverStatus;
        try
        {
            using var status = await this.client.SendAsync(remote, HttpMethod.Get, RemoteRequestPolicy.ApiBase + "/server/status", authenticate: false, cancellationToken: cancellationToken);
            serverStatus = await ReadStringAsync(status, "serverStatus", cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return this.Ok(new RemoteStatusResponseModel(alias, false, null, false, null, exception.Message));
        }

        try
        {
            using var root = await this.client.SendAsync(remote, HttpMethod.Get, RemoteRequestPolicy.ApiBase + "/tree/document/root?skip=0&take=1", cancellationToken: cancellationToken);
            if (!root.IsSuccessStatusCode)
            {
                return this.Ok(new RemoteStatusResponseModel(alias, true, serverStatus, true, null, $"Reading content failed ({(int)root.StatusCode})."));
            }

            using var json = await JsonDocument.ParseAsync(await root.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return this.Ok(new RemoteStatusResponseModel(alias, true, serverStatus, true, json.RootElement.GetProperty("total").GetInt32(), null));
        }
        catch (RemoteAuthenticationException exception)
        {
            return this.Ok(new RemoteStatusResponseModel(alias, true, serverStatus, false, null, exception.Message));
        }
    }

    private static RemoteResponseModel ToResponse(RegisteredRemote remote)
        => new(
            remote.Remote.Alias,
            remote.Remote.Name,
            remote.Remote.BaseUrl,
            remote.Source == RemoteSource.Runtime ? "runtime" : "configuration",
            remote.Validation.IsValid,
            remote.Validation.Errors);

    private static async Task<string?> ReadStringAsync(HttpResponseMessage response, string property, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return json.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
    }
}