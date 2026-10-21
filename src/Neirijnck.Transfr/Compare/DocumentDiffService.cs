using System.Net;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Reads one document (or media item) on both sides, lays it out for the diff view with <see cref="DocumentDiffer"/>
/// (<see cref="MediaDiffer"/>), and looks up the names behind its keys and aliases in this environment's schema.
/// </summary>
public sealed class DocumentDiffService
{
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly LocalMediaReader localMedia;
    private readonly RemoteMediaReader remoteMedia;
    private readonly LocalSchemaNames schemaNames;

    public DocumentDiffService(
        LocalDocumentReader localDocuments,
        RemoteDocumentReader remoteDocuments,
        LocalMediaReader localMedia,
        RemoteMediaReader remoteMedia,
        LocalSchemaNames schemaNames)
    {
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.localMedia = localMedia;
        this.remoteMedia = remoteMedia;
        this.schemaNames = schemaNames;
    }

    /// <summary>
    /// The diff with its names, or null when the document is missing on either side. Other remote failures throw.
    /// </summary>
    public async Task<(DocumentDiff Diff, SchemaNames Names)?> DiffAsync(RemoteDefinition remote, Guid key, CancellationToken cancellationToken)
    {
        var local = await this.localDocuments.ReadAsync(key);
        if (local is null)
        {
            return null;
        }

        string remoteJson;
        try
        {
            remoteJson = await this.remoteDocuments.ReadAsync(remote, key, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a failure: the document simply isn't on the remote; the controller answers 404.
            return null;
        }

        var diff = DocumentDiffer.Diff(local, remoteJson);
        return (diff, await this.schemaNames.ForAsync(diff));
    }

    /// <summary>
    /// The diff of a media item with its names and its files on both sides (where each side serves them), or null when
    /// the media item is missing on either side. Other remote failures throw.
    /// </summary>
    public async Task<(DocumentDiff Diff, SchemaNames Names, IReadOnlyList<MediaFileDiff> Files)?> DiffMediaAsync(
        RemoteDefinition remote,
        Guid key,
        CancellationToken cancellationToken)
    {
        var local = await this.localMedia.ReadAsync(key);
        if (local is null)
        {
            return null;
        }

        string remoteJson;
        try
        {
            remoteJson = await this.remoteMedia.ReadAsync(remote, key, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a failure: the media item simply isn't on the remote; the controller answers 404.
            return null;
        }

        var diff = MediaDiffer.Diff(local, remoteJson);
        return (diff, await this.schemaNames.ForAsync(diff), FilesOf(local, remoteJson, remote.BaseUrl));
    }

    // Each file value on either side, with the address that side serves it at: this site's path as it is, the remote's
    // on the remote's address. A path that is already a full address (another file storage) is kept as it is.
    private static List<MediaFileDiff> FilesOf(string localJson, string remoteJson, string remoteBaseUrl)
    {
        var localFiles = MediaFiles.Of(localJson);
        var remoteFiles = MediaFiles.Of(remoteJson);
        static (string, string?, string?) Place(MediaFile file) => (file.Alias, file.Culture, file.Segment);
        string RemoteUrl(string src) => Uri.TryCreate(src, UriKind.Absolute, out _) ? src : remoteBaseUrl.TrimEnd('/') + src;
        return localFiles.Select(Place).Union(remoteFiles.Select(Place))
            .Select(place =>
            {
                var local = localFiles.FirstOrDefault(file => Place(file) == place);
                var there = remoteFiles.FirstOrDefault(file => Place(file) == place);
                return new MediaFileDiff(place.Item1, place.Item2, place.Item3, local?.Src, there is null ? null : RemoteUrl(there.Src));
            })
            .ToList();
    }
}