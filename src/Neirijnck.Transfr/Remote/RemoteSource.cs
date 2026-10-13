namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Where a remote comes from.
/// </summary>
public enum RemoteSource
{
    /// <summary>The <c>Transfr:Remotes</c> configuration.</summary>
    Configuration,

    /// <summary>Connected at runtime by the current user, kept in memory.</summary>
    Runtime,
}