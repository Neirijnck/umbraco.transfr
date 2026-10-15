using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Tests.Remote;

public sealed class RemoteIdentityTests
{
    [Theory]
    [InlineData("https://localhost:44372", "https://localhost:44372")]
    [InlineData("https://LocalHost:44372/", "https://localhost:44372")]
    [InlineData("HTTPS://staging.example.com:443/", "https://staging.example.com")]
    [InlineData("https://example.com/site/", "https://example.com/site")]
    [InlineData("https://example.com/site?x=1#y", "https://example.com/site")]
    public void Of_SpellsTheSameEnvironmentOneWay(string baseUrl, string expected)
    {
        Assert.Equal(expected, RemoteIdentity.Of(baseUrl));
    }

    [Fact]
    public void Of_KeepsEnvironmentsOnOtherPortsOrPathsApart()
    {
        Assert.NotEqual(RemoteIdentity.Of("https://localhost:44372"), RemoteIdentity.Of("https://localhost:44373"));
        Assert.NotEqual(RemoteIdentity.Of("https://example.com/a"), RemoteIdentity.Of("https://example.com/b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("file:///c:/sites/b")]
    public void Of_RefusesAnythingButAnAbsoluteUrl(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => RemoteIdentity.Of(baseUrl));
    }
}