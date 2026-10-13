using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Controllers;

public class RemotesController : TransfrApiControllerBase
{
    private readonly RemoteRegistry registry;
    private readonly RemoteClient client;

    public RemotesController(RemoteRegistry registry, RemoteClient client)
    {
        this.registry = registry;
        this.client = client;
    }

    [HttpGet("remotes")]
    [ProducesResponseType<IEnumerable<RemoteResponseModel>>(StatusCodes.Status200OK)]
    public IActionResult GetRemotes()
        => this.Ok(this.registry.GetAll().Select(result => new RemoteResponseModel(
            result.Remote.Alias, result.Remote.Name, result.Remote.BaseUrl, result.IsValid, result.Errors)));

    /// <summary>
    /// Tests the connection: is the remote reachable, do the client credentials work, can the API user read content.
    /// </summary>
    [HttpGet("remotes/{alias}/status")]
    [ProducesResponseType<RemoteStatusResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string alias, CancellationToken cancellationToken)
    {
        var remote = this.registry.FindValid(alias);
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