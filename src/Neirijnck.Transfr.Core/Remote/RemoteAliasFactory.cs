using System.Text;

namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// Derives an alias for a remote connected at runtime from its address, e.g. <c>https://staging.example.com</c> gives
/// <c>staging-example-com</c> and <c>https://localhost:44372</c> gives <c>localhost-44372</c>.
/// </summary>
public static class RemoteAliasFactory
{
    private const int MaxLength = 50;

    public static string FromBaseUrl(string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var source = uri.IsDefaultPort ? uri.Host : $"{uri.Host}-{uri.Port}";
        var alias = new StringBuilder(source.Length);
        foreach (var character in source.ToLowerInvariant())
        {
            var allowed = char.IsAsciiLetterOrDigit(character) ? character : '-';
            if (allowed != '-' || (alias.Length > 0 && alias[^1] != '-'))
            {
                alias.Append(allowed);
            }
        }

        var result = alias.ToString().Trim('-');
        return result.Length > MaxLength ? result[..MaxLength].TrimEnd('-') : result;
    }
}