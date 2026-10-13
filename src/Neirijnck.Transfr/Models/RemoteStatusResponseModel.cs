namespace Neirijnck.Transfr.Models;

/// <summary>
/// The result of a connection test against a remote, step by step: reachable, signed in, content readable.
/// </summary>
public sealed record RemoteStatusResponseModel(
    string Alias,
    bool Reachable,
    string? ServerStatus,
    bool Authenticated,
    int? RootDocuments,
    string? Error);