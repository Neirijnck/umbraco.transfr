using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Reads one media item from a remote through its Management API, as <c>GET media/{id}</c> returns it.
/// </summary>
public sealed class RemoteMediaReader
{
    private readonly RemoteClient client;

    public RemoteMediaReader(RemoteClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// The media item's JSON. Throws <see cref="HttpRequestException"/> when the remote doesn't answer with it (404 when
    /// the media item is gone).
    /// </summary>
    public Task<string> ReadAsync(RemoteDefinition remote, Guid key, CancellationToken cancellationToken)
        => this.client.GetStringAsync(remote, $"{RemoteRequestPolicy.ApiBase}/media/{key:D}", cancellationToken);
}