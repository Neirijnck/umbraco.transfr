namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// A file a media item holds: the property value it is in, and its path (e.g. <c>/media/zn1jr2zj/red.png</c>).
/// </summary>
public sealed record MediaFile(string Alias, string? Culture, string? Segment, string Src);