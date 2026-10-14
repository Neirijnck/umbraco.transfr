using System.Text.Json;

namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// Reads the remote Management API's schema responses that the check needs beyond the items themselves.
/// </summary>
public static class ManagementApiSchemaParser
{
    /// <summary>
    /// The document types an <c>item/document-type/search</c> response lists: key and name (the search matches names).
    /// </summary>
    public static IReadOnlyList<(Guid Key, string Name)> ParseDocumentTypeSearch(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString() ?? string.Empty))
            .ToList();
    }

    /// <summary>
    /// The alias of a <c>document-type/{id}</c> response.
    /// </summary>
    public static string? ParseAlias(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("alias", out var alias) ? alias.GetString() : null;
    }
}