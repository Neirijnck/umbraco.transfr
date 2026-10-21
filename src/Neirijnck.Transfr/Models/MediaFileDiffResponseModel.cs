namespace Neirijnck.Transfr.Models;

/// <summary>
/// A file of a media item on both sides, for showing it: the property it is in (Label from this environment's media
/// type, else the alias) and where each side serves it (LocalUrl on this site, RemoteUrl on the remote; null when that
/// side has no file there).
/// </summary>
public sealed record MediaFileDiffResponseModel(string Alias, string Label, string? Culture, string? Segment, string? LocalUrl, string? RemoteUrl);