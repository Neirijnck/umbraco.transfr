namespace Neirijnck.Transfr.Planning;

/// <summary>
/// Who last changed a conflicting document on the remote, and when.
/// </summary>
/// <param name="By">The user's name there (null when the remote didn't give one).</param>
/// <param name="At">When.</param>
/// <param name="What">The kind of change (Save, Publish, …).</param>
public sealed record RemoteChangeInfo(string? By, DateTimeOffset At, string What);