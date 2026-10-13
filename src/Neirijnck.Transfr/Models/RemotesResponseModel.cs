namespace Neirijnck.Transfr.Models;

/// <summary>
/// The current user's remotes, and whether more can be connected at runtime here.
/// </summary>
public sealed record RemotesResponseModel(bool AllowRuntimeRemotes, IReadOnlyList<RemoteResponseModel> Remotes);