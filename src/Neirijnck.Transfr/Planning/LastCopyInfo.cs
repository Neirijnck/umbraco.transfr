namespace Neirijnck.Transfr.Planning;

/// <summary>
/// The latest copy of a document from here to the remote.
/// </summary>
/// <param name="By">The name of the backoffice user here who ran it (null when that user no longer exists).</param>
/// <param name="At">When it was written.</param>
public sealed record LastCopyInfo(string? By, DateTimeOffset At);