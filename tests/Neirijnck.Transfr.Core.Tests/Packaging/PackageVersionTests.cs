using System.Text.Json;
using System.Xml.Linq;

namespace Neirijnck.Transfr.Core.Tests.Packaging;

/// <summary>
/// The package version lives in two files that must change together: the NuGet version (the csproj's VersionPrefix)
/// and the version the backoffice shows (the client's umbraco-package.json).
/// </summary>
public sealed class PackageVersionTests
{
    [Fact]
    public void UmbracoPackageJson_CarriesTheNuGetVersion()
    {
        var prefixes = XDocument.Parse(RepositoryFiles.ReadAllText("src", "Neirijnck.Transfr", "Neirijnck.Transfr.csproj")).Descendants("VersionPrefix").ToList();
        var prefix = Assert.Single(prefixes).Value;
        Assert.Matches(@"^\d+\.\d+\.\d+$", prefix);

        using var manifest = JsonDocument.Parse(RepositoryFiles.ReadAllText("src", "Neirijnck.Transfr", "Client", "public", "umbraco-package.json"));
        Assert.Equal(prefix, manifest.RootElement.GetProperty("version").GetString());
    }
}