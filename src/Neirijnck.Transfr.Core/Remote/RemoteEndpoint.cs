namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// One request the tool may send to a remote's Management API: an HTTP method plus a path template, where
/// <c>{id}</c> stands for exactly one GUID segment.
/// </summary>
public sealed record RemoteEndpoint(string Method, string PathTemplate);