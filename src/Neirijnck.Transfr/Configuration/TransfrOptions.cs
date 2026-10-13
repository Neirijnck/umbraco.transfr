namespace Neirijnck.Transfr.Configuration;

/// <summary>
/// The <c>Transfr</c> configuration section. With no remotes, the package exposes no endpoints and no UI.
/// </summary>
public sealed class TransfrOptions
{
    public const string Section = "Transfr";

    public IList<RemoteOptions> Remotes { get; init; } = [];
}