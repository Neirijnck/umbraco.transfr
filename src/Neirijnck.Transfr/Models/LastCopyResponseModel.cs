namespace Neirijnck.Transfr.Models;

/// <summary>
/// The latest copy of a document from here to the remote: By is the name of the user here who ran it (null when that
/// user no longer exists).
/// </summary>
public sealed record LastCopyResponseModel(string? By, DateTimeOffset At);