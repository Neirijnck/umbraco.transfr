namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// Where a node of one tree level exists, by key. "Local" is this environment, "remote" the one compared with.
/// Whether a node present on both sides has the same content is decided separately (content compare).
/// </summary>
public enum TreeCompareStatus
{
    /// <summary>Under this parent on both sides.</summary>
    InBoth,

    /// <summary>Under this parent locally; nowhere on the remote.</summary>
    OnlyLocal,

    /// <summary>Under this parent on the remote; nowhere locally.</summary>
    OnlyRemote,

    /// <summary>Under this parent locally; on the remote under another parent.</summary>
    ElsewhereOnRemote,

    /// <summary>Under this parent on the remote; locally under another parent.</summary>
    ElsewhereLocally,
}