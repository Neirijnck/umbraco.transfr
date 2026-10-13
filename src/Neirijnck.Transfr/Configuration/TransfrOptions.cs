namespace Neirijnck.Transfr.Configuration;

/// <summary>
/// The <c>Transfr</c> configuration section. With no configured remotes and runtime connections not allowed, the
/// package exposes no endpoints and no UI.
/// </summary>
public sealed class TransfrOptions
{
    public const string Section = "Transfr";

    public IList<RemoteOptions> Remotes { get; init; } = [];

    /// <summary>
    /// Whether administrators may connect a remote at runtime (kept in memory only). Unset means: only in the
    /// Development and Local environments, so a deployed site doesn't get the UI unless it is switched on.
    /// </summary>
    public bool? AllowRuntimeRemotes { get; set; }
}