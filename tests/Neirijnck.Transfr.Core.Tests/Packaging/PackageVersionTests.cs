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
        var project = Path.Combine(RepositoryRoot(), "src", "Neirijnck.Transfr");

        var prefixes = XDocument.Load(Path.Combine(project, "Neirijnck.Transfr.csproj")).Descendants("VersionPrefix").ToList();
        var prefix = Assert.Single(prefixes).Value;
        Assert.Matches(@"^\d+\.\d+\.\d+$", prefix);

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Client", "public", "umbraco-package.json")));
        Assert.Equal(prefix, manifest.RootElement.GetProperty("version").GetString());
    }

    // The tests run from bin/<configuration>/<framework>; the repository root holds the solution file.
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Neirijnck.Transfr.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No Neirijnck.Transfr.slnx above {AppContext.BaseDirectory}: run the tests from the repository.");
    }
}