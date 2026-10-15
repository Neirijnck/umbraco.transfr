namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// The last change to a document's content on a remote, from its audit log.
/// </summary>
/// <param name="UserKey">The user who made it.</param>
/// <param name="At">When.</param>
/// <param name="LogType">What it was (Save, Publish, …), as the audit log names it.</param>
public sealed record RemoteChange(Guid UserKey, DateTimeOffset At, string LogType);