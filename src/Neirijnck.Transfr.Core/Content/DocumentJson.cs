using System.Globalization;
using System.Text.Json.Nodes;

namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Reading a document as the Management API returns it (<c>GET document/{id}</c>), shared by
/// <see cref="ContentComparer"/> and <see cref="DocumentDiffer"/> so the verdict and the diff never disagree.
/// </summary>
internal static class DocumentJson
{
    private const string MultiUrlPicker = "Umbraco.MultiUrlPicker";

    // Arrays inside values whose order carries no meaning, with the fields that identify an item. An array is only
    // sorted when every item is an object carrying those fields, so an editor's own list of the same name keeps its
    // order. Block layouts are not listed: their order is the order on the page.
    private static readonly Dictionary<string, string[]> UnorderedArrays = new(StringComparer.Ordinal)
    {
        ["values"] = ["alias", "culture", "segment"],
        ["contentData"] = ["key"],
        ["settingsData"] = ["key"],
        ["expose"] = ["contentKey", "culture", "segment"],
    };

    // Multi URL picker fields the API derives from the linked item on its own side.
    private static readonly string[] DerivedLinkFields = ["icon", "published", "trashed"];

    public static JsonObject Parse(string json, string parameterName)
        => JsonNode.Parse(json) as JsonObject ?? throw new ArgumentException("A document must be a JSON object.", parameterName);

    public static Guid? IdOf(JsonNode? reference)
        => Guid.TryParse(StringOf(reference?["id"]), out var id) ? id : null;

    public static string? StringOf(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static (string? Culture, string? Segment) CultureAndSegment(JsonObject? entry)
        => (StringOf(entry?["culture"]), StringOf(entry?["segment"]));

    /// <summary>The last save of any variant (null when no variant carries a date).</summary>
    public static DateTimeOffset? LastUpdate(JsonObject document)
        => Entries(document["variants"], withAlias: false)
            .Select(variant => DateTimeOffset.TryParse(StringOf(variant.Entry["updateDate"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateTimeOffset?)null)
            .Max();

    /// <summary>
    /// Variants by culture and segment, or values by alias, culture and segment, in the order they come. Cultures and
    /// segments compare without case.
    /// </summary>
    public static List<(string Key, JsonObject Entry)> Entries(JsonNode? entries, bool withAlias)
    {
        var list = new List<(string Key, JsonObject Entry)>();
        foreach (var entry in (entries as JsonArray ?? []).OfType<JsonObject>())
        {
            var (culture, segment) = CultureAndSegment(entry);
            var alias = withAlias ? StringOf(entry["alias"]) : null;
            list.Add(($"{alias}\u001f{culture?.ToLowerInvariant()}\u001f{segment?.ToLowerInvariant()}", entry));
        }

        return list;
    }

    /// <summary>
    /// The keys of both sides: the local ones in their order, then the remote ones the local side doesn't have.
    /// </summary>
    public static List<T> UnionInOrder<T>(IEnumerable<T> local, IEnumerable<T> remote)
    {
        var seen = new HashSet<T>();
        return local.Concat(remote).Where(seen.Add).ToList();
    }

    /// <summary>
    /// A property value as text that is equal exactly when the content is: see <see cref="Canonical"/>. A missing
    /// value is "null": the API may leave out empty values.
    /// </summary>
    public static string CanonicalText(JsonNode? value, string? editorAlias)
        => CanonicalValue(value, editorAlias)?.ToJsonString() ?? "null";

    public static JsonNode? CanonicalValue(JsonNode? value, string? editorAlias)
    {
        var canonical = Canonical(value, propertyName: null);
        if (string.Equals(editorAlias, MultiUrlPicker, StringComparison.Ordinal) && canonical is JsonArray links)
        {
            foreach (var link in links.OfType<JsonObject>())
            {
                foreach (var field in DerivedLinkFields)
                {
                    link.Remove(field);
                }

                // A document or media link points at its item by key; the URL is that item's URL on this side. An
                // external link's URL is the content itself.
                if (StringOf(link["type"]) is "document" or "media")
                {
                    link.Remove("url");
                }
            }
        }

        return canonical;
    }

    // A copy with object properties in name order and unordered arrays sorted, so equal content gives equal text.
    private static JsonNode? Canonical(JsonNode? node, string? propertyName)
    {
        switch (node)
        {
            case JsonObject source:
            {
                // Property values inside blocks carry their own editor alias.
                var editorAlias = StringOf(source["editorAlias"]);
                var copy = new JsonObject();
                foreach (var (name, child) in source.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    copy[name] = name == "value" && editorAlias is not null ? CanonicalValue(child, editorAlias) : Canonical(child, name);
                }

                return copy;
            }

            case JsonArray source:
            {
                var items = source.Select(item => Canonical(item, propertyName: null)).ToList();
                if (propertyName is not null
                    && UnorderedArrays.TryGetValue(propertyName, out var fields)
                    && items.All(item => item is JsonObject entry && fields.All(entry.ContainsKey)))
                {
                    items = items
                        .OrderBy(item => string.Join('\u001f', fields.Select(field => item![field]?.ToJsonString() ?? "null")), StringComparer.Ordinal)
                        .ThenBy(item => item!.ToJsonString(), StringComparer.Ordinal)
                        .ToList();
                }

                return new JsonArray(items.ToArray());
            }

            default:
                return node?.DeepClone();
        }
    }
}