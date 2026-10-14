using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Reads one document from a remote through its Management API, as <c>GET document/{id}</c> returns it.
/// </summary>
public sealed class RemoteDocumentReader
{
    private readonly RemoteClient client;

    public RemoteDocumentReader(RemoteClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// The document's JSON. Throws <see cref="HttpRequestException"/> when the remote doesn't answer with it (404 when
    /// the document is gone).
    /// </summary>
    public Task<string> ReadAsync(RemoteDefinition remote, Guid key, CancellationToken cancellationToken)
        => this.client.GetStringAsync(remote, $"{RemoteRequestPolicy.ApiBase}/document/{key:D}", cancellationToken);
}