using Neirijnck.Transfr.Core.History;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// The latest copy of a document or media item between here and the remote, either way.
/// </summary>
/// <param name="By">The name of the backoffice user here who ran it (null when that user no longer exists).</param>
/// <param name="At">When it was written.</param>
/// <param name="Direction">Which way it was copied.</param>
public sealed record LastCopyInfo(string? By, DateTimeOffset At, CopyDirection Direction);