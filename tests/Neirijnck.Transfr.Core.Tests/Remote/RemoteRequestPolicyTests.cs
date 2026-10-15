using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Tests.Remote;

public sealed class RemoteRequestPolicyTests
{
    private const string Api = RemoteRequestPolicy.ApiBase;

    // Every endpoint the tool may call. Adding one means adding it here too: a deliberate, reviewed change.
    private static readonly string[] ExpectedEndpoints =
    [
        "POST " + Api + "/security/back-office/token",
        "GET " + Api + "/server/status",
        "GET " + Api + "/tree/document/root",
        "GET " + Api + "/tree/document/children",
        "GET " + Api + "/item/document",
        "GET " + Api + "/item/media",
        "GET " + Api + "/document/{id}",
        "GET " + Api + "/document-type/{id}",
        "GET " + Api + "/data-type/{id}",
        "GET " + Api + "/item/document-type/search",
        "GET " + Api + "/document/{id}/audit-log",
        "GET " + Api + "/item/user",
        "POST " + Api + "/document",
        "POST " + Api + "/document/create-and-publish",
        "PUT " + Api + "/document/{id}",
        "PUT " + Api + "/document/{id}/update-and-publish",
        "PUT " + Api + "/document/sort",
    ];

    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT"];

    // Route fragments of operations the tool must never perform on a remote.
    private static readonly string[] ForbiddenFragments =
    [
        "unpublish", "move", "recycle-bin", "copy", "sort-children", "delete", "user", "user-group", "client-credentials", "permission",
    ];

    [Fact]
    public void Endpoints_AreExactlyTheReviewedList()
    {
        var actual = RemoteRequestPolicy.Endpoints.Select(e => $"{e.Method} {e.PathTemplate}");

        Assert.Equal(ExpectedEndpoints.Order(), actual.Order());
    }

    [Fact]
    public void Endpoints_UseOnlyGetPostAndPut()
    {
        var methods = RemoteRequestPolicy.Endpoints.Select(e => e.Method).Distinct().ToList();

        Assert.All(methods, method => Assert.Contains(method, AllowedMethods));
    }

    // The one reviewed exception to the fragment rule: reading user names, so a conflict can say who last changed a
    // document on the remote. Every other user endpoint stays forbidden.
    private static readonly string[] AllowedDespiteFragment = ["GET " + Api + "/item/user"];

    [Fact]
    public void Endpoints_ContainNoForbiddenOperation()
    {
        foreach (var endpoint in RemoteRequestPolicy.Endpoints.Where(e => !AllowedDespiteFragment.Contains($"{e.Method} {e.PathTemplate}")))
        {
            var segments = endpoint.PathTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Assert.DoesNotContain(segments, segment => ForbiddenFragments.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData("POST", Api + "/security/back-office/token")]
    [InlineData("GET", Api + "/tree/document/root?skip=0&take=100")]
    [InlineData("get", Api + "/TREE/document/children?parentId=3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/item/document/")]
    [InlineData("GET", Api + "/item/media?id=3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/document-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/data-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/item/document-type/search?query=Home")]
    [InlineData("POST", Api + "/document")]
    [InlineData("POST", Api + "/document/create-and-publish")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/update-and-publish")]
    [InlineData("PUT", Api + "/document/sort")]
    [InlineData("GET", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/audit-log?orderDirection=Descending&skip=0&take=20")]
    [InlineData("GET", Api + "/item/user?id=3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    public void IsAllowed_AcceptsListedRequests(string method, string path)
    {
        Assert.True(RemoteRequestPolicy.IsAllowed(method, path));
    }

    [Theory]
    [InlineData("DELETE", Api + "/tree/document/root")]
    [InlineData("PATCH", Api + "/tree/document/root")]
    [InlineData("PUT", Api + "/tree/document/root")]
    [InlineData("GET", Api + "/security/back-office/token")]
    [InlineData("GET", Api + "/tree/document/root/extra")]
    [InlineData("GET", Api + "/tree/document")]
    [InlineData("GET", Api + "/tree/media/root")]
    [InlineData("GET", Api + "/tree/document/../../user")]
    [InlineData("GET", Api + "/tree/document/%2E%2E/root")]
    [InlineData("GET", "/umbraco/management/api/v2/tree/document/root")]
    [InlineData("GET", "/tree/document/root")]
    [InlineData("GET", "")]
    [InlineData("DELETE", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("POST", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("PUT", Api + "/document")]
    [InlineData("PUT", Api + "/document/sort/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/publish")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/publish-with-descendants")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/sort-children")]
    [InlineData("PUT", Api + "/document/root/sort-children")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/move")]
    [InlineData("POST", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/copy")]
    [InlineData("PATCH", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/patch")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/domains")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/public-access")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/unpublish")]
    [InlineData("PUT", Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/move-to-recycle-bin")]
    [InlineData("GET", Api + "/document/root")]
    [InlineData("GET", Api + "/user")]
    [InlineData("GET", Api + "/user/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("POST", Api + "/item/user")]
    [InlineData("GET", Api + "/item/user-group")]
    [InlineData("DELETE", Api + "/document-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("PUT", Api + "/document-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("POST", Api + "/document-type")]
    [InlineData("DELETE", Api + "/data-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("PUT", Api + "/data-type/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("GET", Api + "/tree/document-type/root")]
    public void IsAllowed_RejectsEverythingElse(string method, string path)
    {
        Assert.False(RemoteRequestPolicy.IsAllowed(method, path));
    }

    [Fact]
    public void EnsureAllowed_PassesAListedRequestToTheRemote()
    {
        var remote = new Uri("https://localhost:44372");

        RemoteRequestPolicy.EnsureAllowed("GET", new Uri(remote, Api + "/tree/document/root?take=10"), remote);
    }

    [Theory]
    [InlineData("https://other.example.com" + Api + "/tree/document/root")]
    [InlineData("http://localhost:44372" + Api + "/tree/document/root")]
    [InlineData("https://localhost:44373" + Api + "/tree/document/root")]
    [InlineData("https://localhost:44372" + Api + "/media/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    [InlineData("https://other.example.com" + Api + "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")]
    public void EnsureAllowed_ThrowsForOtherHostsAndUnlistedPaths(string requestUri)
    {
        var remote = new Uri("https://localhost:44372");

        Assert.Throws<RemoteRequestNotAllowedException>(() => RemoteRequestPolicy.EnsureAllowed("GET", new Uri(requestUri), remote));
    }

    [Fact]
    public void EnsureAllowed_ThrowsForAnUnlistedMethodOnAListedPath()
    {
        var remote = new Uri("https://localhost:44372");

        Assert.Throws<RemoteRequestNotAllowedException>(() => RemoteRequestPolicy.EnsureAllowed("DELETE", new Uri(remote, Api + "/tree/document/root"), remote));
    }

    [Theory]
    [InlineData("/document/{id}", "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b", true)]
    [InlineData("/document/{id}", "/document/3F2A1C4E-5B6D-4E7F-8A9B-0C1D2E3F4A5B", true)]
    [InlineData("/document/{id}", "/document/3f2a1c4e5b6d4e7f8a9b0c1d2e3f4a5b", false)]
    [InlineData("/document/{id}", "/document/root", false)]
    [InlineData("/document/{id}", "/document", false)]
    [InlineData("/document/{id}/audit-log", "/document/3f2a1c4e-5b6d-4e7f-8a9b-0c1d2e3f4a5b/audit-log", true)]
    public void Matches_TreatsIdAsExactlyOneGuidSegment(string template, string path, bool expected)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, RemoteRequestPolicy.Matches(template, segments));
    }
}