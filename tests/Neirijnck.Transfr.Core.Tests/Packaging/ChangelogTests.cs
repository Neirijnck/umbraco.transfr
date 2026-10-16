using System.Xml.Linq;

namespace Neirijnck.Transfr.Core.Tests.Packaging;

/// <summary>
/// The release workflow takes a version's release notes from its section in CHANGELOG.md and refuses to publish
/// without one. This catches a missing section when the version is raised, instead of when the version is tagged.
/// </summary>
public sealed class ChangelogTests
{
    [Fact]
    public void Changelog_HasOneSectionWithNotesForThePackageVersion()
    {
        var prefixes = XDocument.Parse(RepositoryFiles.ReadAllText("src", "Neirijnck.Transfr", "Neirijnck.Transfr.csproj")).Descendants("VersionPrefix").ToList();
        var heading = $"## [{Assert.Single(prefixes).Value}]";

        var lines = RepositoryFiles.ReadAllText("CHANGELOG.md").Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        // Read like the workflow does: the section starts at the line beginning with the heading and ends before the
        // next second-level heading.
        var starts = lines.Select((line, index) => (line, index)).Where(item => item.line.StartsWith(heading, StringComparison.Ordinal)).ToList();
        var start = Assert.Single(starts).index;
        var notes = lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)).ToList();

        Assert.Contains(notes, line => !string.IsNullOrWhiteSpace(line));
    }
}