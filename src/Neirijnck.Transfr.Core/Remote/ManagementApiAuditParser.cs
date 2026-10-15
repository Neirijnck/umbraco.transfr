using System.Globalization;
using System.Text.Json;

namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// Reads who last changed a document on a remote: its audit log (<c>document/{id}/audit-log</c>, newest first) and the
/// names of the users it mentions (<c>item/user</c>). Shapes measured on an Umbraco 17.7.0 test site.
/// </summary>
public static class ManagementApiAuditParser
{
    // Entries that change a document's content or publish state. Sort, Move, Copy, Open, Notify and the rest don't:
    // sorting also bumps the document's update date, but it isn't what someone wrote.
    private static readonly HashSet<string> ContentChanges = new(StringComparer.Ordinal)
    {
        "New", "Save", "SaveVariant", "Publish", "PublishVariant", "Unpublish", "UnpublishVariant", "RollBack",
    };

    /// <summary>The newest content change in an audit log page that lists newest first, or null when it has none.</summary>
    /// <exception cref="JsonException">Not valid JSON.</exception>
    public static RemoteChange? LatestChange(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            var logType = item.TryGetProperty("logType", out var type) ? type.GetString() : null;
            if (logType is null || !ContentChanges.Contains(logType)
                || !item.TryGetProperty("user", out var user) || !user.TryGetProperty("id", out var id) || !id.TryGetGuid(out var userKey)
                || !item.TryGetProperty("timestamp", out var timestamp)
                || !DateTimeOffset.TryParse(timestamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                continue;
            }

            return new RemoteChange(userKey, at, logType);
        }

        return null;
    }

    /// <summary>User key to name, from an <c>item/user</c> response; users without a name are left out.</summary>
    /// <exception cref="JsonException">Not valid JSON.</exception>
    public static IReadOnlyDictionary<Guid, string> UserNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = new Dictionary<Guid, string>();
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.TryGetGuid(out var key)
                && item.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } text)
            {
                names[key] = text;
            }
        }

        return names;
    }
}