namespace Neirijnck.Transfr.Transfer;

/// <summary>Where a transfer reads the files of the media items it copies: the source's media storage.</summary>
internal interface IMediaFileSource
{
    /// <summary>Requests this source sent so far (0 for this environment).</summary>
    int Requests { get; }

    /// <summary>
    /// Opens a media file by the path its media value names (e.g. <c>/media/zn1jr2zj/red.png</c>). Throws
    /// <see cref="IOException"/> or <see cref="HttpRequestException"/> when it can't be read.
    /// </summary>
    Task<Stream> OpenAsync(string src, CancellationToken cancellationToken);
}