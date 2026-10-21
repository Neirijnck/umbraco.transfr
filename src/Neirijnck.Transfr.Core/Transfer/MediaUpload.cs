namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// A file to upload to the target as a temporary file (<c>POST temporary-file</c>, multipart <c>Id</c> + <c>File</c>)
/// before the media write that names it.
/// </summary>
/// <param name="TemporaryFileId">The id the upload gets and the media write refers to.</param>
/// <param name="Src">The file here, as the media item's value names it (e.g. <c>/media/zn1jr2zj/red.png</c>).</param>
/// <param name="FileName">The file name to upload it under: the last part of <paramref name="Src"/>, so the target stores
/// it at the same path (the path follows from the media key, the property and the file name).</param>
public sealed record MediaUpload(Guid TemporaryFileId, string Src, string FileName);