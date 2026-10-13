using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Tests.Remote;

public sealed class RemoteDefinitionValidatorTests
{
    private static readonly RemoteDefinition Valid = new("staging", "Staging", "https://staging.example.com", "umbraco-back-office-transfr", "secret");

    [Fact]
    public void Validate_AcceptsAWellFormedRemote()
    {
        var result = RemoteDefinitionValidator.Validate([Valid]).Single();

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("https://staging.example.com/")]
    [InlineData("https://localhost:44372")]
    [InlineData("http://localhost:44372")]
    [InlineData("http://127.0.0.1:8080")]
    public void Validate_AcceptsSiteRootUrls(string baseUrl)
    {
        var result = RemoteDefinitionValidator.Validate([Valid with { BaseUrl = baseUrl }]).Single();

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("http://staging.example.com", "https")]
    [InlineData("https://staging.example.com/umbraco", "site root")]
    [InlineData("https://staging.example.com/?x=1", "site root")]
    [InlineData("ftp://staging.example.com", "absolute http(s)")]
    [InlineData("staging.example.com", "absolute http(s)")]
    [InlineData("", "absolute http(s)")]
    public void Validate_RejectsUnusableBaseUrls(string baseUrl, string expectedError)
    {
        var result = RemoteDefinitionValidator.Validate([Valid with { BaseUrl = baseUrl }]).Single();

        Assert.Contains(result.Errors, error => error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("-staging")]
    [InlineData("stag ing")]
    [InlineData("")]
    public void Validate_RejectsBadAliases(string alias)
    {
        var result = RemoteDefinitionValidator.Validate([Valid with { Alias = alias }]).Single();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RequiresNameClientIdAndSecret()
    {
        var result = RemoteDefinitionValidator.Validate([Valid with { Name = " ", ClientId = "", ClientSecret = "" }]).Single();

        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Validate_MarksEveryRemoteSharingAnAliasInvalid()
    {
        var results = RemoteDefinitionValidator.Validate([Valid, Valid with { Alias = "STAGING", Name = "Other" }, Valid with { Alias = "live" }]);

        Assert.Equal([false, false, true], results.Select(r => r.IsValid));
    }
}