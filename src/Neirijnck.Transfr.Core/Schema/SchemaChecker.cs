using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// Checks the schema behind a document type on both sides, by key: the type, its compositions, the data types of its
/// properties, and the element types those data types allow in blocks, each compared on what a copy depends on. Names,
/// icons, descriptions, tabs and sort orders are never read. Both sides are read as the Management API serves them
/// (<c>document-type/{id}</c>, <c>data-type/{id}</c>).
/// </summary>
public static class SchemaChecker
{
    /// <param name="documentTypeKey">The document type of the content to copy.</param>
    /// <param name="readLocal">Reads an item locally; null when it doesn't exist here.</param>
    /// <param name="readRemote">Reads an item on the remote; null when it doesn't exist there.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    public static async Task<SchemaCheck> CheckAsync(
        Guid documentTypeKey,
        Func<SchemaItemKind, Guid, CancellationToken, Task<string?>> readLocal,
        Func<SchemaItemKind, Guid, CancellationToken, Task<string?>> readRemote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readLocal);
        ArgumentNullException.ThrowIfNull(readRemote);

        var problems = new List<SchemaProblem>();
        var queue = new Queue<(SchemaItemKind Kind, Guid Key)>();
        var seen = new HashSet<(SchemaItemKind Kind, Guid Key)>();
        Enqueue(SchemaItemKind.DocumentType, documentTypeKey);

        while (queue.Count > 0)
        {
            var (kind, key) = queue.Dequeue();
            var local = Parse(await readLocal(kind, key, cancellationToken));
            var remote = Parse(await readRemote(kind, key, cancellationToken));

            // A missing item stops the walk on that branch: a copy is impossible anyway, and its parts would only repeat it.
            if (local is null || remote is null)
            {
                var present = local ?? remote;
                problems.Add(new SchemaProblem(
                    kind,
                    key,
                    DocumentJson.StringOf(present?["name"]),
                    IsElement(present),
                    local is null ? SchemaProblemReason.MissingLocally : SchemaProblemReason.MissingOnRemote,
                    []));
                continue;
            }

            var details = kind == SchemaItemKind.DocumentType ? CompareDocumentTypes(local, remote) : CompareDataTypes(local, remote);
            if (details.Count > 0)
            {
                problems.Add(new SchemaProblem(kind, key, DocumentJson.StringOf(local["name"]), IsElement(local), SchemaProblemReason.Differs, details));
            }

            foreach (var reference in ReferencesOf(kind, local).Concat(ReferencesOf(kind, remote)))
            {
                Enqueue(reference.Kind, reference.Key);
            }
        }

        return new SchemaCheck(documentTypeKey, problems);

        void Enqueue(SchemaItemKind kind, Guid key)
        {
            if (seen.Add((kind, key)))
            {
                queue.Enqueue((kind, key));
            }
        }
    }

    private static JsonObject? Parse(string? json) => json is null ? null : DocumentJson.Parse(json, nameof(json));

    private static bool IsElement(JsonObject? item) => item?["isElement"] is JsonValue value && value.TryGetValue<bool>(out var isElement) && isElement;

    // What an item depends on: a type's compositions and its properties' data types; the element types a data type's
    // configuration allows in blocks (block list, block grid and rich text all name them the same way).
    private static IEnumerable<(SchemaItemKind Kind, Guid Key)> ReferencesOf(SchemaItemKind kind, JsonObject item)
    {
        if (kind == SchemaItemKind.DocumentType)
        {
            foreach (var key in IdsOf(item["compositions"], "documentType"))
            {
                yield return (SchemaItemKind.DocumentType, key);
            }

            foreach (var key in IdsOf(item["properties"], "dataType"))
            {
                yield return (SchemaItemKind.DataType, key);
            }

            yield break;
        }

        foreach (var key in ElementTypesIn(item["values"]))
        {
            yield return (SchemaItemKind.DocumentType, key);
        }
    }

    private static IEnumerable<Guid> ElementTypesIn(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject item:
                foreach (var (name, child) in item)
                {
                    if (name is "contentElementTypeKey" or "settingsElementTypeKey" && Guid.TryParse(DocumentJson.StringOf(child), out var key))
                    {
                        yield return key;
                    }

                    foreach (var nested in ElementTypesIn(child))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray list:
                foreach (var nested in list.SelectMany(ElementTypesIn))
                {
                    yield return nested;
                }

                break;
        }
    }

    // The ids under each entry's reference property, e.g. compositions[].documentType.id.
    private static IEnumerable<Guid> IdsOf(JsonNode? entries, string reference)
        => (entries as JsonArray ?? []).OfType<JsonObject>()
            .Select(entry => DocumentJson.IdOf(entry[reference]))
            .OfType<Guid>();

    private static List<string> CompareDocumentTypes(JsonObject local, JsonObject remote)
    {
        var details = new List<string>();
        CompareText(details, "alias", local["alias"], remote["alias"]);
        CompareFlag(details, "element type", local["isElement"], remote["isElement"]);
        CompareFlag(details, "varies by culture", local["variesByCulture"], remote["variesByCulture"]);
        CompareFlag(details, "varies by segment", local["variesBySegment"], remote["variesBySegment"]);
        CompareFlag(details, "allowed at the root", local["allowedAsRoot"], remote["allowedAsRoot"]);
        CompareIds(details, "compositions differ", IdsOf(local["compositions"], "documentType"), IdsOf(remote["compositions"], "documentType"));
        CompareIds(details, "allowed child types differ", IdsOf(local["allowedDocumentTypes"], "documentType"), IdsOf(remote["allowedDocumentTypes"], "documentType"));
        CompareIds(details, "allowed templates differ", IdsOfReferences(local["allowedTemplates"]), IdsOfReferences(remote["allowedTemplates"]));
        if (DocumentJson.IdOf(local["defaultTemplate"]) != DocumentJson.IdOf(remote["defaultTemplate"]))
        {
            details.Add("the default template differs");
        }

        var localProperties = PropertiesByAlias(local);
        var remoteProperties = PropertiesByAlias(remote);
        foreach (var alias in DocumentJson.UnionInOrder(localProperties.Keys, remoteProperties.Keys))
        {
            var localProperty = localProperties.GetValueOrDefault(alias);
            var remoteProperty = remoteProperties.GetValueOrDefault(alias);
            if (localProperty is null)
            {
                details.Add($"property '{alias}' exists only on the remote");
                continue;
            }

            if (remoteProperty is null)
            {
                details.Add($"property '{alias}' is missing on the remote");
                continue;
            }

            if (DocumentJson.IdOf(localProperty["dataType"]) != DocumentJson.IdOf(remoteProperty["dataType"]))
            {
                details.Add($"property '{alias}' uses another data type");
            }

            CompareFlag(details, $"property '{alias}' varies by culture", localProperty["variesByCulture"], remoteProperty["variesByCulture"]);
            CompareFlag(details, $"property '{alias}' varies by segment", localProperty["variesBySegment"], remoteProperty["variesBySegment"]);
            CompareFlag(details, $"property '{alias}' is mandatory", localProperty["validation"]?["mandatory"], remoteProperty["validation"]?["mandatory"]);
            CompareText(details, $"property '{alias}' pattern", localProperty["validation"]?["regEx"], remoteProperty["validation"]?["regEx"]);
        }

        return details;
    }

    // allowedTemplates holds plain references ({ "id": ... }); the generic reader above expects a nested reference.
    private static IEnumerable<Guid> IdsOfReferences(JsonNode? entries)
        => (entries as JsonArray ?? []).OfType<JsonObject>().Select(entry => DocumentJson.IdOf(entry)).OfType<Guid>();

    private static Dictionary<string, JsonObject> PropertiesByAlias(JsonObject type)
    {
        var properties = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in (type["properties"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (DocumentJson.StringOf(property["alias"]) is { } alias)
            {
                properties.TryAdd(alias, property);
            }
        }

        return properties;
    }

    private static List<string> CompareDataTypes(JsonObject local, JsonObject remote)
    {
        var details = new List<string>();
        CompareText(details, "editor", local["editorAlias"], remote["editorAlias"]);
        CompareText(details, "editor UI", local["editorUiAlias"], remote["editorUiAlias"]);

        var localValues = DocumentJson.Entries(local["values"], withAlias: true);
        var remoteValues = DocumentJson.Entries(remote["values"], withAlias: true);
        foreach (var key in DocumentJson.UnionInOrder(localValues.Select(v => v.Key), remoteValues.Select(v => v.Key)))
        {
            var localValue = localValues.FirstOrDefault(v => v.Key == key).Entry;
            var remoteValue = remoteValues.FirstOrDefault(v => v.Key == key).Entry;
            if (!string.Equals(DocumentJson.CanonicalText(localValue?["value"], null), DocumentJson.CanonicalText(remoteValue?["value"], null), StringComparison.Ordinal))
            {
                details.Add($"setting '{DocumentJson.StringOf((localValue ?? remoteValue)!["alias"])}' differs");
            }
        }

        return details;
    }

    private static void CompareText(List<string> details, string what, JsonNode? local, JsonNode? remote)
    {
        var localText = DocumentJson.StringOf(local);
        var remoteText = DocumentJson.StringOf(remote);
        if (!string.Equals(localText, remoteText, StringComparison.Ordinal))
        {
            details.Add($"{what}: {localText ?? "none"} here, {remoteText ?? "none"} on the remote");
        }
    }

    private static void CompareFlag(List<string> details, string what, JsonNode? local, JsonNode? remote)
    {
        var localFlag = local is JsonValue l && l.TryGetValue<bool>(out var lv) && lv;
        var remoteFlag = remote is JsonValue r && r.TryGetValue<bool>(out var rv) && rv;
        if (localFlag != remoteFlag)
        {
            details.Add($"{what}: {(localFlag ? "yes" : "no")} here, {(remoteFlag ? "yes" : "no")} on the remote");
        }
    }

    private static void CompareIds(List<string> details, string what, IEnumerable<Guid> local, IEnumerable<Guid> remote)
    {
        if (!local.ToHashSet().SetEquals(remote))
        {
            details.Add(what);
        }
    }
}