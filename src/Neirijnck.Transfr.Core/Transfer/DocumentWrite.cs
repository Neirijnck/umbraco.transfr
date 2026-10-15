namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// One request that writes to the target's Management API.
/// </summary>
/// <param name="Method">POST or PUT.</param>
/// <param name="Path">The path from the site root (it passes the remote request allowlist).</param>
/// <param name="Body">The JSON body.</param>
public sealed record DocumentWrite(string Method, string Path, string Body);