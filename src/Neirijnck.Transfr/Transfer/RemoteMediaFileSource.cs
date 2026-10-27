using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// A remote's media files as the source: downloaded from the path the media value names, as the site serves it
/// (anonymously, measured: no token is sent). Read whole into memory, since the temporary file it becomes here is read
/// again by Umbraco. A file the remote doesn't serve publicly can't be copied.
/// </summary>
internal sealed class RemoteMediaFileSource : IMediaFileSource
{
    private readonly RemoteDefinition remote;
    private readonly RemoteClient client;
    private int requests;

    public RemoteMediaFileSource(RemoteDefinition remote, RemoteClient client)
    {
        this.remote = remote;
        this.client = client;
    }

    public int Requests => this.requests;

    public async Task<Stream> OpenAsync(string src, CancellationToken cancellationToken)
    {
        this.requests++;
        using var response = await this.client.SendAsync(this.remote, HttpMethod.Get, src, authenticate: false, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"The remote answered {(int)response.StatusCode} for the file {src}.", null, response.StatusCode);
        }

        return new MemoryStream(await response.Content.ReadAsByteArrayAsync(cancellationToken), writable: false);
    }
}