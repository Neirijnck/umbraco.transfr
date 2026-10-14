using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Lays one document out on both sides for a diff view: every document field and property value, and per block of a
/// block list, block grid or rich text value its own status and values. Whether something differs is decided exactly
/// as <see cref="ContentComparer"/> decides it.
/// </summary>
public static class DocumentDiffer
{
    // The text is shown, never embedded in HTML by us (the backoffice escapes it), so keep it readable.
    private static readonly JsonSerializerOptions DisplayOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <exception cref="JsonException">Either side is not valid JSON.</exception>
    /// <exception cref="ArgumentException">Either side is not a JSON object.</exception>
    public static DocumentDiff Diff(string localJson, string remoteJson)
    {
        var local = DocumentJson.Parse(localJson, nameof(localJson));
        var remote = DocumentJson.Parse(remoteJson, nameof(remoteJson));

        var fields = new List<FieldDiff>
        {
            new(ContentDifferenceKind.DocumentType, null, null, IdText(local["documentType"]), IdText(remote["documentType"])),
            new(ContentDifferenceKind.Template, null, null, IdText(local["template"]), IdText(remote["template"])),
        };

        var localVariants = DocumentJson.Entries(local["variants"], withAlias: false);
        var remoteVariants = DocumentJson.Entries(remote["variants"], withAlias: false);
        foreach (var key in DocumentJson.UnionInOrder(localVariants.Select(v => v.Key), remoteVariants.Select(v => v.Key)))
        {
            var localVariant = localVariants.FirstOrDefault(v => v.Key == key).Entry;
            var remoteVariant = remoteVariants.FirstOrDefault(v => v.Key == key).Entry;
            var (culture, segment) = DocumentJson.CultureAndSegment(localVariant ?? remoteVariant);
            fields.Add(new(ContentDifferenceKind.Name, culture, segment, DocumentJson.StringOf(localVariant?["name"]), DocumentJson.StringOf(remoteVariant?["name"])));
            fields.Add(new(ContentDifferenceKind.State, culture, segment, DocumentJson.StringOf(localVariant?["state"]), DocumentJson.StringOf(remoteVariant?["state"])));
        }

        return new DocumentDiff(fields, DiffValues(local["values"], remote["values"]));
    }

    private static string? IdText(JsonNode? reference) => DocumentJson.IdOf(reference)?.ToString("D");

    // Property values (of the document, a block or block settings) by alias, culture and segment.
    private static List<PropertyDiff> DiffValues(JsonNode? local, JsonNode? remote)
    {
        var localValues = DocumentJson.Entries(local, withAlias: true);
        var remoteValues = DocumentJson.Entries(remote, withAlias: true);

        var properties = new List<PropertyDiff>();
        foreach (var key in DocumentJson.UnionInOrder(localValues.Select(v => v.Key), remoteValues.Select(v => v.Key)))
        {
            var localValue = localValues.FirstOrDefault(v => v.Key == key).Entry;
            var remoteValue = remoteValues.FirstOrDefault(v => v.Key == key).Entry;
            var entry = (localValue ?? remoteValue)!;
            var (culture, segment) = DocumentJson.CultureAndSegment(entry);
            var value = DiffValue(
                localValue?["value"],
                remoteValue?["value"],
                DocumentJson.StringOf(localValue?["editorAlias"]),
                DocumentJson.StringOf(remoteValue?["editorAlias"]));
            properties.Add(new PropertyDiff(DocumentJson.StringOf(entry["alias"]) ?? string.Empty, culture, segment, DocumentJson.StringOf(entry["editorAlias"]), value));
        }

        return properties;
    }

    private static ValueDiff DiffValue(JsonNode? local, JsonNode? remote, string? localEditor, string? remoteEditor)
    {
        var differs = !string.Equals(
            DocumentJson.CanonicalText(local, localEditor),
            DocumentJson.CanonicalText(remote, remoteEditor),
            StringComparison.Ordinal);

        // Rich text: the markup as text, the blocks inside it as blocks.
        if ((local is JsonObject localRichText && localRichText.ContainsKey("markup"))
            || (remote is JsonObject remoteRichText && remoteRichText.ContainsKey("markup")))
        {
            return new ValueDiff(differs, DocumentJson.StringOf(local?["markup"]), DocumentJson.StringOf(remote?["markup"]), DiffBlocks(local?["blocks"], remote?["blocks"]));
        }

        if (IsBlockValue(local) || IsBlockValue(remote))
        {
            return new ValueDiff(differs, null, null, DiffBlocks(local, remote));
        }

        return new ValueDiff(differs, Display(local, localEditor), Display(remote, remoteEditor), []);
    }

    private static bool IsBlockValue(JsonNode? value) => value is JsonObject block && block["contentData"] is JsonArray;

    private static string? Display(JsonNode? value, string? editorAlias)
        => value switch
        {
            null => null,
            JsonValue text when text.TryGetValue<string>(out var s) => s,
            _ => DocumentJson.CanonicalValue(value, editorAlias)?.ToJsonString(DisplayOptions),
        };

    private static List<BlockDiff> DiffBlocks(JsonNode? local, JsonNode? remote)
    {
        var localLayout = LayoutOf(local?["layout"]);
        var remoteLayout = LayoutOf(remote?["layout"]);
        var localContent = ByKey(local?["contentData"]);
        var remoteContent = ByKey(remote?["contentData"]);
        var localSettings = ByKey(local?["settingsData"]);
        var remoteSettings = ByKey(remote?["settingsData"]);

        // A block on both sides is "moved" when it is outside the longest run that kept its order on the page, so
        // one block moved to the end doesn't mark every block it passed.
        var shared = localLayout.Where(item => remoteLayout.Any(other => other.ContentKey == item.ContentKey)).ToList();
        var inOrder = OrderRun.LongestIncreasing(shared.Select(item => remoteLayout.FindIndex(other => other.ContentKey == item.ContentKey)).ToList());
        var moved = shared.Where((_, index) => !inOrder.Contains(index)).Select(item => item.ContentKey).ToHashSet();

        var blocks = new List<BlockDiff>();
        foreach (var key in DocumentJson.UnionInOrder(localLayout.Select(item => item.ContentKey), remoteLayout.Select(item => item.ContentKey)))
        {
            var localIndex = localLayout.FindIndex(item => item.ContentKey == key);
            var remoteIndex = remoteLayout.FindIndex(item => item.ContentKey == key);
            var localBlock = localContent.GetValueOrDefault(key);
            var remoteBlock = remoteContent.GetValueOrDefault(key);
            var localBlockSettings = localIndex >= 0 && localLayout[localIndex].SettingsKey is { } localSettingsKey ? localSettings.GetValueOrDefault(localSettingsKey) : null;
            var remoteBlockSettings = remoteIndex >= 0 && remoteLayout[remoteIndex].SettingsKey is { } remoteSettingsKey ? remoteSettings.GetValueOrDefault(remoteSettingsKey) : null;

            var status = localBlock is null ? BlockDiffStatus.OnlyRemote
                : remoteBlock is null ? BlockDiffStatus.OnlyLocal
                : !SameContent(localBlock, remoteBlock) || !SameContent(localBlockSettings, remoteBlockSettings) ? BlockDiffStatus.Changed
                : moved.Contains(key) ? BlockDiffStatus.Moved
                : BlockDiffStatus.Same;

            blocks.Add(new BlockDiff(
                key,
                status,
                TypeKeyOf(localBlock ?? remoteBlock),
                TypeKeyOf(localBlockSettings ?? remoteBlockSettings),
                localIndex >= 0 ? localIndex : null,
                remoteIndex >= 0 ? remoteIndex : null,
                DiffValues(localBlock?["values"], remoteBlock?["values"]),
                DiffValues(localBlockSettings?["values"], remoteBlockSettings?["values"])));
        }

        return blocks;
    }

    private static Guid? TypeKeyOf(JsonObject? blockData)
        => Guid.TryParse(DocumentJson.StringOf(blockData?["contentTypeKey"]), out var key) ? key : null;

    private static bool SameContent(JsonObject? local, JsonObject? remote)
        => string.Equals(DocumentJson.CanonicalText(local, null), DocumentJson.CanonicalText(remote, null), StringComparison.Ordinal);

    // The blocks in page order: every layout item, areas of a grid included (depth first, as they appear).
    private static List<(Guid ContentKey, Guid? SettingsKey)> LayoutOf(JsonNode? layout)
    {
        var items = new List<(Guid ContentKey, Guid? SettingsKey)>();
        Walk(layout);
        return items;

        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject item:
                    if (Guid.TryParse(DocumentJson.StringOf(item["contentKey"]), out var contentKey))
                    {
                        items.Add((contentKey, Guid.TryParse(DocumentJson.StringOf(item["settingsKey"]), out var settingsKey) ? settingsKey : null));
                    }

                    foreach (var (_, child) in item)
                    {
                        Walk(child);
                    }

                    break;
                case JsonArray list:
                    foreach (var child in list)
                    {
                        Walk(child);
                    }

                    break;
            }
        }
    }

    private static Dictionary<Guid, JsonObject> ByKey(JsonNode? data)
    {
        var byKey = new Dictionary<Guid, JsonObject>();
        foreach (var item in (data as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Guid.TryParse(DocumentJson.StringOf(item["key"]), out var key))
            {
                byKey.TryAdd(key, item);
            }
        }

        return byKey;
    }
}