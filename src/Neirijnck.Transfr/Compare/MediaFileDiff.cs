namespace Neirijnck.Transfr.Compare;

/// <summary>
/// A file of a media item on both sides: the value it is in, and the address each side serves it at (null when that
/// side has no file there).
/// </summary>
public sealed record MediaFileDiff(string Alias, string? Culture, string? Segment, string? LocalUrl, string? RemoteUrl);