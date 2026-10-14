using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.References;

/// <summary>
/// Finds what a document's content points at: documents, media items and members, by key. It reads the document as
/// the Management API returns it (<c>GET document/{id}</c>), so it works the same on both sides; the remote has no
/// "references from" endpoint. Pickers are read per editor, rich text from its links and images, blocks value by value
/// (nested blocks included); a value of any other editor is searched for UDIs.
/// </summary>
public static partial class ReferenceExtractor
{
    /// <exception cref="System.Text.Json.JsonException">Not valid JSON.</exception>
    /// <exception cref="ArgumentException">Not a JSON object.</exception>
    public static IReadOnlyList<ContentReference> Extract(string documentJson)
    {
        var document = DocumentJson.Parse(documentJson, nameof(documentJson));
        var references = new List<ContentReference>();
        var seen = new HashSet<ContentReference>();
        foreach (var entry in (document["values"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var alias = DocumentJson.StringOf(entry["alias"]) ?? string.Empty;
            var (culture, segment) = DocumentJson.CultureAndSegment(entry);
            foreach (var (kind, key) in FromValue(entry["value"], DocumentJson.StringOf(entry["editorAlias"])))
            {
                var reference = new ContentReference(kind, key, alias, culture, segment);
                if (seen.Add(reference))
                {
                    references.Add(reference);
                }
            }
        }

        return references;
    }

    private static IEnumerable<(ReferenceKind Kind, Guid Key)> FromValue(JsonNode? value, string? editorAlias)
    {
        switch (editorAlias)
        {
            case "Umbraco.ContentPicker":
                return KeyOrUdi(value, ReferenceKind.Document);
            case "Umbraco.MemberPicker":
                return KeyOrUdi(value, ReferenceKind.Member);
            case "Umbraco.MediaPicker3":
                return Items(value).Select(item => Guid.TryParse(DocumentJson.StringOf(item["mediaKey"]), out var key) ? (ReferenceKind.Media, key) : default)
                    .Where(found => found != default);
            case "Umbraco.MultiUrlPicker":
                // A link to a document or media item names it by key; an external link names nothing.
                return Items(value).Select(item => (Kind: KindOf(DocumentJson.StringOf(item["type"])), Key: DocumentJson.StringOf(item["unique"])))
                    .Where(link => link.Kind is not null && Guid.TryParse(link.Key, out _))
                    .Select(link => (link.Kind!.Value, Guid.Parse(link.Key!)));
            case "Umbraco.MultiNodeTreePicker":
                // The editor value is [{ type, unique }] with the UDI entity type "document", "media" or "member"
                // (measured in 17.7.0: saving type "content" fails with "Unknown entity type").
                return value is JsonArray
                    ? Items(value).Select(item => (Kind: KindOf(DocumentJson.StringOf(item["type"])), Key: DocumentJson.StringOf(item["unique"])))
                        .Where(entry => entry.Kind is not null && Guid.TryParse(entry.Key, out _))
                        .Select(entry => (entry.Kind!.Value, Guid.Parse(entry.Key!)))
                    : UdisIn(value);
            case "Umbraco.RichText":
                return RichText(value);
        }

        return IsBlockValue(value) ? Blocks(value) : UdisIn(value);
    }

    private static IEnumerable<JsonObject> Items(JsonNode? value) => (value as JsonArray ?? []).OfType<JsonObject>();

    private static ReferenceKind? KindOf(string? type)
        => type switch
        {
            "document" => ReferenceKind.Document,
            "media" => ReferenceKind.Media,
            "member" => ReferenceKind.Member,
            _ => null,
        };

    // A plain key (the editor's own kind) or a UDI (which names its kind).
    private static IEnumerable<(ReferenceKind Kind, Guid Key)> KeyOrUdi(JsonNode? value, ReferenceKind kind)
    {
        var text = DocumentJson.StringOf(value);
        if (Guid.TryParse(text, out var key))
        {
            return [(kind, key)];
        }

        return UdisIn(value);
    }

    private static IEnumerable<(ReferenceKind Kind, Guid Key)> RichText(JsonNode? value)
    {
        var markup = DocumentJson.StringOf(value?["markup"]) ?? DocumentJson.StringOf(value) ?? string.Empty;
        foreach (Match anchor in AnchorTag().Matches(markup))
        {
            var link = LocalLink().Match(anchor.Value);
            if (!link.Success)
            {
                continue;
            }

            // A local link by key needs its type attribute to say what it points at; a UDI names it itself.
            var target = link.Groups["target"].Value;
            if (Guid.TryParse(target, out var key) && TypeAttribute().Match(anchor.Value) is { Success: true } type)
            {
                yield return (KindOf(type.Groups["type"].Value.ToLowerInvariant())!.Value, key);
            }
            else if (ParseUdi(target) is { } udi)
            {
                yield return udi;
            }
        }

        foreach (Match dataUdi in DataUdiAttribute().Matches(markup))
        {
            if (ParseUdi(dataUdi.Groups["udi"].Value) is { } udi)
            {
                yield return udi;
            }
        }

        foreach (var reference in Blocks(value?["blocks"]))
        {
            yield return reference;
        }
    }

    private static bool IsBlockValue(JsonNode? value) => value is JsonObject block && block["contentData"] is JsonArray;

    // Every value of every block (content and settings), each read with its own editor, so nested blocks are followed.
    private static IEnumerable<(ReferenceKind Kind, Guid Key)> Blocks(JsonNode? value)
    {
        var data = Items(value?["contentData"]).Concat(Items(value?["settingsData"]));
        foreach (var entry in data.SelectMany(block => Items(block["values"])))
        {
            foreach (var reference in FromValue(entry["value"], DocumentJson.StringOf(entry["editorAlias"])))
            {
                yield return reference;
            }
        }
    }

    // Any UDI in any string of the value: the fallback for editors not read on purpose.
    private static IEnumerable<(ReferenceKind Kind, Guid Key)> UdisIn(JsonNode? value)
    {
        switch (value)
        {
            case JsonValue text when text.TryGetValue<string>(out var s):
                foreach (Match match in Udi().Matches(s))
                {
                    if (ParseUdi(match.Value) is { } udi)
                    {
                        yield return udi;
                    }
                }

                break;
            case JsonObject item:
                foreach (var udi in item.SelectMany(property => UdisIn(property.Value)))
                {
                    yield return udi;
                }

                break;
            case JsonArray list:
                foreach (var udi in list.SelectMany(UdisIn))
                {
                    yield return udi;
                }

                break;
        }
    }

    private static (ReferenceKind Kind, Guid Key)? ParseUdi(string text)
    {
        var match = Udi().Match(text);
        return match.Success && KindOf(match.Groups["type"].Value) is { } kind && Guid.TryParseExact(match.Groups["key"].Value, "N", out var key)
            ? (kind, key)
            : null;
    }

    [GeneratedRegex(@"umb://(?<type>document|media|member)/(?<key>[0-9a-fA-F]{32})")]
    private static partial Regex Udi();

    [GeneratedRegex(@"<a\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTag();

    [GeneratedRegex(@"\{localLink:(?<target>[^}]+)\}")]
    private static partial Regex LocalLink();

    [GeneratedRegex(@"\btype=""(?<type>document|media)""", RegexOptions.IgnoreCase)]
    private static partial Regex TypeAttribute();

    [GeneratedRegex(@"\bdata-udi=""(?<udi>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex DataUdiAttribute();
}