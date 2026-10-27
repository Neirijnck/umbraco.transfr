namespace Neirijnck.Transfr.Models;

/// <summary>
/// The latest copy of a document or media item between here and the remote: By is the name of the user here who ran it
/// (null when that user no longer exists), Direction ToRemote or FromRemote.
/// </summary>
public sealed record LastCopyResponseModel(string? By, DateTimeOffset At, string Direction);