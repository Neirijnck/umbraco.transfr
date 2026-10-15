using System.Text.Json;

namespace Neirijnck.Transfr.Core.Tree;

/// <summary>
/// Reads the remote Management API's document tree responses (<c>tree/document/root|children</c> and
/// <c>item/document</c>) into tree nodes.
/// </summary>
public static class ManagementApiTreeParser
{
    public const string UnnamedNode = "(no name)";

    /// <summary>
    /// One page of a tree level. Items arrive ordered by sort order, so a node's sort index is its position in the
    /// level: <paramref name="skip"/> plus its position on the page.
    /// </summary>
    /// <param name="json">The response body.</param>
    /// <param name="skip">The skip the page was requested with.</param>
    /// <param name="preferredCulture">The culture whose name to show for culture-variant nodes.</param>
    public static (int Total, IReadOnlyList<TreeNode> Nodes) ParseTreePage(string json, int skip, string? preferredCulture)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var nodes = root.GetProperty("items")
            .EnumerateArray()
            .Select((item, index) => new TreeNode(
                item.GetProperty("id").GetGuid(),
                NameOf(item, preferredCulture),
                skip + index,
                item.GetProperty("hasChildren").GetBoolean(),
                DocumentTypeKeyOf(item),
                DocumentTypeIconOf(item)))
            .ToList();

        return (root.GetProperty("total").GetInt32(), nodes);
    }

    /// <summary>
    /// Where the requested documents (<c>item/document</c>) or media items (<c>item/media</c>, the same shape) are: key to
    /// parent key (null = the root). The API leaves out unknown keys, so a key missing from the result doesn't exist. A
    /// trashed item counts as missing: the recycle bin is not a place in the tree (measured: it is listed with
    /// <c>isTrashed</c> true and the recycle bin as its parent).
    /// </summary>
    public static IReadOnlyDictionary<Guid, Guid?> ParseItemParents(string json)
        => ParseItemLocations(json).ToDictionary(pair => pair.Key, pair => pair.Value.ParentKey);

    /// <summary>
    /// <see cref="ParseItemParents"/>, plus whether each item is published in some culture (a document whose variant
    /// state is Published or PublishedPendingChanges; media items carry no state and count as not published).
    /// </summary>
    public static IReadOnlyDictionary<Guid, ItemLocation> ParseItemLocations(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .EnumerateArray()
            .Where(item => !item.GetProperty("isTrashed").GetBoolean())
            .ToDictionary(
                item => item.GetProperty("id").GetGuid(),
                item => new ItemLocation(
                    item.GetProperty("parent") is { ValueKind: JsonValueKind.Object } parent ? parent.GetProperty("id").GetGuid() : null,
                    IsPublished(item)));
    }

    private static bool IsPublished(JsonElement item)
        => item.TryGetProperty("variants", out var variants)
            && variants.ValueKind == JsonValueKind.Array
            && variants.EnumerateArray().Any(variant => variant.TryGetProperty("state", out var state)
                && state.GetString() is "Published" or "PublishedPendingChanges");

    private static Guid? DocumentTypeKeyOf(JsonElement item)
        => item.TryGetProperty("documentType", out var type)
            && type.ValueKind == JsonValueKind.Object
            && type.TryGetProperty("id", out var id)
            && id.TryGetGuid(out var key)
                ? key
                : null;

    private static string? DocumentTypeIconOf(JsonElement item)
        => item.TryGetProperty("documentType", out var type)
            && type.ValueKind == JsonValueKind.Object
            && type.TryGetProperty("icon", out var icon)
            && icon.ValueKind == JsonValueKind.String
                ? icon.GetString()
                : null;

    // Invariant nodes have one variant with culture null; variant nodes one per culture.
    private static string NameOf(JsonElement item, string? preferredCulture)
    {
        var variants = item.GetProperty("variants").EnumerateArray().ToList();
        var chosen = variants.FirstOrDefault(v => v.GetProperty("culture").ValueKind == JsonValueKind.Null);
        if (chosen.ValueKind == JsonValueKind.Undefined && preferredCulture is not null)
        {
            chosen = variants.FirstOrDefault(v => string.Equals(v.GetProperty("culture").GetString(), preferredCulture, StringComparison.OrdinalIgnoreCase));
        }

        if (chosen.ValueKind == JsonValueKind.Undefined && variants.Count > 0)
        {
            chosen = variants[0];
        }

        var name = chosen.ValueKind == JsonValueKind.Undefined ? null : chosen.GetProperty("name").GetString();
        return string.IsNullOrWhiteSpace(name) ? UnnamedNode : name;
    }
}