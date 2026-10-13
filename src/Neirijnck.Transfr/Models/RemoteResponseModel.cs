namespace Neirijnck.Transfr.Models;

/// <summary>
/// A remote as the backoffice sees it: never the client id or secret. Source is "configuration" or "runtime".
/// </summary>
public sealed record RemoteResponseModel(string Alias, string Name, string BaseUrl, string Source, bool IsValid, IReadOnlyList<string> Errors);