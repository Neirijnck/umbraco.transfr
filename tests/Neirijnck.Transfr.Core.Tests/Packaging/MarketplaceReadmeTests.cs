namespace Neirijnck.Transfr.Core.Tests.Packaging;

/// <summary>
/// The marketplace shows umbraco-marketplace-readme.md instead of the NuGet readme: the README's part for users (from
/// the title up to the repository layout, without the logo the marketplace shows itself), then links for the rest. It is
/// a copy, so it must follow every change to that part of the README.
/// </summary>
public sealed class MarketplaceReadmeTests
{
    private const string UserPartStart = "# Transfr";
    private const string UserPartEnd = "## Repository layout";

    [Fact]
    public void MarketplaceReadme_StartsWithTheReadmeUserPart()
    {
        var readme = RepositoryFiles.ReadAllText("README.md");
        var start = readme.IndexOf(UserPartStart + "\r\n", StringComparison.Ordinal);
        var end = readme.IndexOf(UserPartEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"The README has no '{UserPartStart}' title before '{UserPartEnd}'.");
        var userPart = readme[start..end];

        // Sanity guard on the extraction: the part runs from the title through the roadmap.
        Assert.Contains("## Safety", userPart, StringComparison.Ordinal);
        Assert.Contains("## Roadmap", userPart, StringComparison.Ordinal);

        Assert.StartsWith(userPart, RepositoryFiles.ReadAllText("umbraco-marketplace-readme.md"), StringComparison.Ordinal);
    }
}