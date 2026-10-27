using Umbraco.Cms.Core.IO;

namespace Neirijnck.Transfr.Transfer;

/// <summary>This environment's media storage as the source of media files.</summary>
internal sealed class LocalMediaFileSource : IMediaFileSource
{
    private readonly MediaFileManager mediaFiles;

    public LocalMediaFileSource(MediaFileManager mediaFiles)
    {
        this.mediaFiles = mediaFiles;
    }

    public int Requests => 0;

    public Task<Stream> OpenAsync(string src, CancellationToken cancellationToken)
        => Task.FromResult(this.mediaFiles.FileSystem.OpenFile(this.mediaFiles.FileSystem.GetRelativePath(src)));
}