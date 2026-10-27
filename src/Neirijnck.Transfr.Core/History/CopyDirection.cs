namespace Neirijnck.Transfr.Core.History;

/// <summary>Which way a copy goes, seen from this environment. Stored by name in the copy history.</summary>
public enum CopyDirection
{
    /// <summary>From this environment to the remote: the remote is the target.</summary>
    ToRemote,

    /// <summary>From the remote to this environment: this environment is the target.</summary>
    FromRemote,
}