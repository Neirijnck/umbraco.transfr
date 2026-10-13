using System.Text.RegularExpressions;

namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// Checks configured remotes before any of them is used. A remote with errors is never contacted.
/// </summary>
public static partial class RemoteDefinitionValidator
{
    /// <summary>
    /// Validates every remote, in configuration order. Duplicate aliases make every remote that shares the alias
    /// invalid, so the tool never has to guess which one is meant.
    /// </summary>
    public static IReadOnlyList<RemoteValidationResult> Validate(IReadOnlyList<RemoteDefinition> remotes)
    {
        ArgumentNullException.ThrowIfNull(remotes);

        var duplicates = remotes
            .GroupBy(remote => remote.Alias ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return remotes
            .Select(remote =>
            {
                var errors = ValidateOne(remote);
                if (duplicates.Contains(remote.Alias ?? string.Empty))
                {
                    errors.Add($"Alias '{remote.Alias}' is used by more than one remote.");
                }

                return new RemoteValidationResult(remote, errors);
            })
            .ToList();
    }

    private static List<string> ValidateOne(RemoteDefinition remote)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(remote.Alias) || !AliasPattern().IsMatch(remote.Alias))
        {
            errors.Add("Alias must be 1-50 characters: lowercase letters, digits and hyphens, starting with a letter or digit.");
        }

        if (string.IsNullOrWhiteSpace(remote.Name))
        {
            errors.Add("Name is required.");
        }

        if (!Uri.TryCreate(remote.BaseUrl, UriKind.Absolute, out var baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttps && baseUrl.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add("BaseUrl must be an absolute http(s) URL.");
        }
        else
        {
            if (baseUrl.AbsolutePath != "/" || !string.IsNullOrEmpty(baseUrl.Query) || !string.IsNullOrEmpty(baseUrl.Fragment))
            {
                errors.Add("BaseUrl must be the site root, without path, query or fragment.");
            }

            // The client secret travels with every token request: plain http only on this machine.
            if (baseUrl.Scheme == Uri.UriSchemeHttp && !baseUrl.IsLoopback)
            {
                errors.Add("BaseUrl must use https unless it points to this machine.");
            }
        }

        if (string.IsNullOrWhiteSpace(remote.ClientId))
        {
            errors.Add("ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(remote.ClientSecret))
        {
            errors.Add("ClientSecret is required.");
        }

        return errors;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex AliasPattern();
}