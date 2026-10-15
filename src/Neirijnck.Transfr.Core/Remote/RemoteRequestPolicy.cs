namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// The only requests the tool may send to a remote. Nothing is ever deleted, unpublished or moved, so the list holds
/// GET, POST and PUT only, and every addition is a deliberate change guarded by a unit test.
/// The remote's API user group (no Delete, Unpublish or Move) is the second line of defence.
/// </summary>
public static class RemoteRequestPolicy
{
    public const string ApiBase = "/umbraco/management/api/v1";

    public static IReadOnlyList<RemoteEndpoint> Endpoints { get; } =
    [
        new("POST", ApiBase + "/security/back-office/token"),
        new("GET", ApiBase + "/server/status"),
        new("GET", ApiBase + "/tree/document/root"),
        new("GET", ApiBase + "/tree/document/children"),
        new("GET", ApiBase + "/item/document"),
        new("GET", ApiBase + "/item/media"),
        new("GET", ApiBase + "/document/{id}"),
        new("GET", ApiBase + "/document-type/{id}"),
        new("GET", ApiBase + "/data-type/{id}"),
        new("GET", ApiBase + "/item/document-type/search"),

        // Writes, for a transfer: create or update a document (publishing its cultures in the same request), and sort
        // a parent's children. Nothing that deletes, unpublishes, moves or copies.
        new("POST", ApiBase + "/document"),
        new("POST", ApiBase + "/document/create-and-publish"),
        new("PUT", ApiBase + "/document/{id}"),
        new("PUT", ApiBase + "/document/{id}/update-and-publish"),
        new("PUT", ApiBase + "/document/sort"),
    ];

    /// <summary>
    /// Throws unless the request goes to the remote's own scheme, host and port, with a listed method and path.
    /// Every request to a remote passes through this check before it is sent.
    /// </summary>
    public static void EnsureAllowed(string method, Uri requestUri, Uri remoteBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(remoteBaseUrl);

        if (!requestUri.IsAbsoluteUri
            || Uri.Compare(requestUri, remoteBaseUrl, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
        {
            throw new RemoteRequestNotAllowedException($"{method} {requestUri} does not go to the remote {remoteBaseUrl}.");
        }

        if (!IsAllowed(method, requestUri.PathAndQuery))
        {
            throw new RemoteRequestNotAllowedException($"{method} {requestUri.AbsolutePath} is not on the allowlist.");
        }
    }

    /// <summary>
    /// Whether a request may be sent. The query string is ignored; the path must match a template segment by segment
    /// (case-insensitive), with <c>{id}</c> matching exactly one GUID. Because every segment is compared exactly,
    /// dot segments, percent-encoding and unlisted methods can never match.
    /// </summary>
    public static bool IsAllowed(string method, string pathAndQuery)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(pathAndQuery);

        var segments = pathAndQuery.Split('?', 2)[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Endpoints.Any(endpoint =>
            string.Equals(endpoint.Method, method, StringComparison.OrdinalIgnoreCase) && Matches(endpoint.PathTemplate, segments));
    }

    internal static bool Matches(string template, string[] segments)
    {
        var templateSegments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != segments.Length)
        {
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var matches = templateSegments[i] == "{id}"
                ? Guid.TryParseExact(segments[i], "D", out _)
                : string.Equals(templateSegments[i], segments[i], StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }
}