using System.Text.Json.Nodes;

namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Compares one document on both sides, each as the Management API returns it (<c>GET document/{id}</c>). Only what a
/// copy would change counts: the document type, the template, each variant's name and publish state, and each
/// property value. Dates, flags, icons and ids that are not content are never read.
/// </summary>
public static class ContentComparer
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

    /// <exception cref="System.Text.Json.JsonException">Either side is not valid JSON.</exception>
    /// <exception cref="ArgumentException">Either side is not a JSON object.</exception>
    public static ContentComparison Compare(string localJson, string remoteJson)
    {
        var local = ParseDocument(localJson, nameof(localJson));
        var remote = ParseDocument(remoteJson, nameof(remoteJson));
        var differences = new List<ContentDifference>();

        if (IdOf(local["documentType"]) != IdOf(remote["documentType"]))
        {
            differences.Add(new ContentDifference(ContentDifferenceKind.DocumentType));
        }

        if (IdOf(local["template"]) != IdOf(remote["template"]))
        {
            differences.Add(new ContentDifference(ContentDifferenceKind.Template));
        }

        var localVariants = Index(local["variants"], withAlias: false);
        var remoteVariants = Index(remote["variants"], withAlias: false);
        foreach (var key in UnionOf(localVariants.Keys, remoteVariants.Keys))
        {
            var localVariant = localVariants.GetValueOrDefault(key);
            var remoteVariant = remoteVariants.GetValueOrDefault(key);
            var (culture, segment) = CultureAndSegment(localVariant ?? remoteVariant);

            if (!string.Equals(StringOf(localVariant?["name"]), StringOf(remoteVariant?["name"]), StringComparison.Ordinal))
            {
                differences.Add(new ContentDifference(ContentDifferenceKind.Name, Culture: culture, Segment: segment));
            }

            if (!string.Equals(StringOf(localVariant?["state"]), StringOf(remoteVariant?["state"]), StringComparison.Ordinal))
            {
                differences.Add(new ContentDifference(ContentDifferenceKind.State, Culture: culture, Segment: segment));
            }
        }

        var localValues = Index(local["values"], withAlias: true);
        var remoteValues = Index(remote["values"], withAlias: true);
        foreach (var key in UnionOf(localValues.Keys, remoteValues.Keys))
        {
            var localValue = localValues.GetValueOrDefault(key);
            var remoteValue = remoteValues.GetValueOrDefault(key);

            // A value missing on one side compares as null: the API may leave out empty values.
            var localCanonical = CanonicalValue(localValue?["value"], StringOf(localValue?["editorAlias"]))?.ToJsonString() ?? "null";
            var remoteCanonical = CanonicalValue(remoteValue?["value"], StringOf(remoteValue?["editorAlias"]))?.ToJsonString() ?? "null";
            if (!string.Equals(localCanonical, remoteCanonical, StringComparison.Ordinal))
            {
                var entry = localValue ?? remoteValue;
                var (culture, segment) = CultureAndSegment(entry);
                differences.Add(new ContentDifference(ContentDifferenceKind.Value, StringOf(entry?["alias"]), culture, segment));
            }
        }

        return new ContentComparison(differences);
    }

    private static JsonObject ParseDocument(string json, string parameterName)
        => JsonNode.Parse(json) as JsonObject ?? throw new ArgumentException("A document must be a JSON object.", parameterName);

    private static Guid? IdOf(JsonNode? reference)
        => Guid.TryParse(StringOf(reference?["id"]), out var id) ? id : null;

    private static string? StringOf(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static (string? Culture, string? Segment) CultureAndSegment(JsonObject? entry)
        => (StringOf(entry?["culture"]), StringOf(entry?["segment"]));

    // Variants by culture and segment, or values by alias, culture and segment. Cultures and segments compare without
    // case; the sorted keys give the differences a stable order.
    private static SortedDictionary<string, JsonObject> Index(JsonNode? entries, bool withAlias)
    {
        var index = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var entry in (entries as JsonArray ?? []).OfType<JsonObject>())
        {
            var (culture, segment) = CultureAndSegment(entry);
            var alias = withAlias ? StringOf(entry["alias"]) : null;
            index[$"{alias}\u001f{culture?.ToLowerInvariant()}\u001f{segment?.ToLowerInvariant()}"] = entry;
        }

        return index;
    }

    private static SortedSet<string> UnionOf(IEnumerable<string> left, IEnumerable<string> right)
        => new(left.Concat(right), StringComparer.Ordinal);

    private static JsonNode? CanonicalValue(JsonNode? value, string? editorAlias)
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