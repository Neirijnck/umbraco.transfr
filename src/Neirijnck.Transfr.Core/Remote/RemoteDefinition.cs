namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// A remote environment as configured locally: where it is and the API user's client credentials.
/// </summary>
public sealed record RemoteDefinition(string Alias, string Name, string BaseUrl, string ClientId, string ClientSecret);