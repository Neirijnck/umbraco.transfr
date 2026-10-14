namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document type with the same alias on both sides under another key: evidence that the remote was installed
/// separately, so nothing can be copied to or from it.
/// </summary>
public sealed record SeparateInstallResponseModel(string Alias, string Name, Guid LocalKey, Guid RemoteKey);