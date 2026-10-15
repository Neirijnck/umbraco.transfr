namespace Neirijnck.Transfr.Models;

/// <summary>
/// Who last changed a document on the remote: By is the user's name there (null when it couldn't be read), What the
/// audit log type (Save, Publish, SaveVariant, …).
/// </summary>
public sealed record RemoteChangeResponseModel(string? By, DateTimeOffset At, string What);