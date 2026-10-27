using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Tests.Remote;

/// <summary>
/// Shapes measured on an Umbraco 17.7.0 test site (document/{id}/audit-log?orderDirection=Descending, item/user).
/// </summary>
public sealed class ManagementApiAuditParserTests
{
    private static readonly Guid Api = new("72eabca6-3215-42be-9837-e4b2b9a4f9be");
    private static readonly Guid Admin = new("1e70f841-c261-413b-abb2-2d68cdb96094");

    [Fact]
    public void LatestChange_SkipsASortToTheNewestSaveOrPublish()
    {
        const string json = """
            {"total":3,"items":[
              {"user":{"id":"72eabca6-3215-42be-9837-e4b2b9a4f9be"},"timestamp":"2026-10-15T10:01:14.4691204+00:00","logType":"Sort","comment":"Sorting content performed by user","parameters":null},
              {"user":{"id":"1e70f841-c261-413b-abb2-2d68cdb96094"},"timestamp":"2026-10-15T09:21:11.1000000+00:00","logType":"Publish","comment":null,"parameters":null},
              {"user":{"id":"1e70f841-c261-413b-abb2-2d68cdb96094"},"timestamp":"2026-10-15T09:21:10.8610092+00:00","logType":"Save","comment":null,"parameters":null}]}
            """;

        var change = ManagementApiAuditParser.LatestChange(json);

        Assert.Equal(new RemoteChange(Admin, DateTimeOffset.Parse("2026-10-15T09:21:11.1+00:00", System.Globalization.CultureInfo.InvariantCulture), "Publish"), change);
    }

    [Fact]
    public void LatestChange_IsNullWithoutAContentChange()
    {
        const string json = """{"total":1,"items":[{"user":{"id":"72eabca6-3215-42be-9837-e4b2b9a4f9be"},"timestamp":"2026-10-15T10:01:14+00:00","logType":"Sort"}]}""";

        Assert.Null(ManagementApiAuditParser.LatestChange(json));
        Assert.Null(ManagementApiAuditParser.LatestChange("""{"total":0,"items":[]}"""));
    }

    // The same rule reads this environment's own audit items, by their AuditType name.
    [Theory]
    [InlineData("Save", true)]
    [InlineData("PublishVariant", true)]
    [InlineData("RollBack", true)]
    [InlineData("Sort", false)]
    [InlineData("Move", false)]
    [InlineData("save", false)]
    public void IsContentChange_KnowsWhatChangesContent(string logType, bool expected)
    {
        Assert.Equal(expected, ManagementApiAuditParser.IsContentChange(logType));
    }

    [Fact]
    public void UserNames_MapsKeysToNames()
    {
        const string json = """
            [{"avatarUrls":[],"kind":"Default","name":"Transfr Admin","id":"1e70f841-c261-413b-abb2-2d68cdb96094","flags":[]},
             {"avatarUrls":[],"kind":"Api","name":"Transfr Remote","id":"72eabca6-3215-42be-9837-e4b2b9a4f9be","flags":[]},
             {"avatarUrls":[],"kind":"Default","name":"","id":"00000000-0000-4000-8000-000000000001","flags":[]}]
            """;

        var names = ManagementApiAuditParser.UserNames(json);

        Assert.Equal(2, names.Count);
        Assert.Equal("Transfr Admin", names[Admin]);
        Assert.Equal("Transfr Remote", names[Api]);
    }
}