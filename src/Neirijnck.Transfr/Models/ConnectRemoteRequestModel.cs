namespace Neirijnck.Transfr.Models;

/// <summary>
/// A remote to connect at runtime. The secret is kept in server memory only and never returned.
/// </summary>
public sealed class ConnectRemoteRequestModel
{
    public required string Name { get; init; }

    public required string BaseUrl { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}