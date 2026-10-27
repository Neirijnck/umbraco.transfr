namespace Neirijnck.Transfr.Models;

/// <summary>
/// The current user's remotes, whether more can be connected at runtime here, and whether copying from a remote to here
/// is allowed.
/// </summary>
public sealed record RemotesResponseModel(bool AllowRuntimeRemotes, IReadOnlyList<RemoteResponseModel> Remotes, bool AllowPull);