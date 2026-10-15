namespace Neirijnck.Transfr.Core.Tests.Packaging;

/// <summary>
/// Reads files from the repository the tests run in, for checks on files that ship but aren't code.
/// </summary>
internal static class RepositoryFiles
{
    /// <summary>The repository root: the tests run from bin/configuration/framework, below the solution file.</summary>
    public static string Root()
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

    public static string ReadAllText(params string[] path) => File.ReadAllText(Path.Combine([Root(), .. path]));
}