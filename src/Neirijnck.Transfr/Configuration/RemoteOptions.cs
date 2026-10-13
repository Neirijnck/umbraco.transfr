namespace Neirijnck.Transfr.Configuration;

/// <summary>
/// One remote environment. Lives only in local, untracked configuration: it holds a client secret.
/// </summary>
public sealed class RemoteOptions
{
    public string Alias { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}