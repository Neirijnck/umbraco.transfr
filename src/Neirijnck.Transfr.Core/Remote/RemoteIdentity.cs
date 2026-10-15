namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// Which environment a remote is, for the copy history: its base URL in one spelling (scheme and host lower case,
/// default port left out, no trailing slash). Not the alias: a runtime connection gets a new alias every time, while
/// the copies it made are still the copies of that environment.
/// </summary>
public static class RemoteIdentity
{
    /// <exception cref="ArgumentException">Not an absolute http(s) URL.</exception>
    public static string Of(string baseUrl)
    {
        // The scheme check also refuses a path: on Linux "/x" parses as an absolute file URI.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException($"'{baseUrl}' is not an absolute http(s) URL.", nameof(baseUrl));
        }

        // Uri already lower-cases scheme and host and drops a default port from GetLeftPart.
        return (uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath).TrimEnd('/');
    }
}