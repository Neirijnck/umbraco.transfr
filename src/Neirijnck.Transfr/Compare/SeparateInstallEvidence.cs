namespace Neirijnck.Transfr.Compare;

/// <summary>
/// A document type with the same alias on both sides but another key: the remote was installed separately, so keys
/// can't identify anything between the two and copying is refused as a whole.
/// </summary>
public sealed record SeparateInstallEvidence(string Alias, string Name, Guid LocalKey, Guid RemoteKey);