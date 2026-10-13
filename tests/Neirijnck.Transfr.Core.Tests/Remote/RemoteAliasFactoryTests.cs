using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Tests.Remote;

public sealed class RemoteAliasFactoryTests
{
    [Theory]
    [InlineData("https://staging.example.com", "staging-example-com")]
    [InlineData("https://staging.example.com/", "staging-example-com")]
    [InlineData("https://localhost:44372", "localhost-44372")]
    [InlineData("https://STAGING.Example.com:443", "staging-example-com")]
    [InlineData("http://127.0.0.1:8080", "127-0-0-1-8080")]
    [InlineData("https://my--site.example.com", "my-site-example-com")]
    [InlineData("not a url", "")]
    public void FromBaseUrl_DerivesAValidAlias(string baseUrl, string expected)
    {
        Assert.Equal(expected, RemoteAliasFactory.FromBaseUrl(baseUrl));
    }

    [Fact]
    public void FromBaseUrl_ProducesAliasesTheValidatorAccepts()
    {
        var alias = RemoteAliasFactory.FromBaseUrl("https://a-very-long-subdomain-name.another-long-part.example-company.com:8443");
        var remote = new RemoteDefinition(alias, "Name", "https://a-very-long-subdomain-name.another-long-part.example-company.com:8443", "id", "secret");

        Assert.True(RemoteDefinitionValidator.Validate([remote]).Single().IsValid, alias);
        Assert.True(alias.Length <= 50, alias);
    }
}