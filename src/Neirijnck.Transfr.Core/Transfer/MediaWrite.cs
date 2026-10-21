namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// The write of one media item to the target: the files to upload first, then one request.
/// </summary>
/// <param name="Uploads">The files to upload first, each under its temporary file id; empty when no file changes.</param>
/// <param name="Write">The create or update that names those uploads.</param>
public sealed record MediaWrite(IReadOnlyList<MediaUpload> Uploads, DocumentWrite Write);