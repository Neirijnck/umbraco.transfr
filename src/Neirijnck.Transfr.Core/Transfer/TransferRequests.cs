using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// The requests that carry out a plan on the target, built from the source document as <c>GET document/{id}</c> returns
/// it (its values post back unchanged: measured in 17.7.0). The shapes were measured on a test site with the API user.
/// </summary>
public static class TransferRequests
{
    /// <summary>
    /// The write that makes the target's copy equal to the source: create (key, parent and document type kept) or update,
    /// and publish the row's cultures in the same request when it has any.
    /// </summary>
    /// <exception cref="ArgumentException">The row is not a document to create or update.</exception>
    public static DocumentWrite For(PlanRow row, string sourceJson)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind != PlanItemKind.Document || row.Outcome is not (PlanOutcome.Create or PlanOutcome.Update or PlanOutcome.Conflict))
        {
            throw new ArgumentException($"{row.Kind} {row.Key} is {row.Outcome}: nothing to write.", nameof(row));
        }

        // Writing the whole source would overwrite every field the user left alone.
        if (row.Fields is not null)
        {
            throw new ArgumentException($"Document {row.Key} is a field-level copy: write it with {nameof(ForFields)}.", nameof(row));
        }

        var source = DocumentJson.Parse(sourceJson, nameof(sourceJson));

        // A culture not created here isn't created there either, nor are its values sent. Invariant values (culture
        // null) always go, also in culture-variant content.
        var variants = DocumentJson.Entries(source["variants"], withAlias: false)
            .Select(variant => variant.Entry)
            .Where(variant => DocumentJson.StringOf(variant["state"]) != "NotCreated")
            .ToList();
        var cultures = variants.Select(variant => DocumentJson.StringOf(variant["culture"])).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = (source["values"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(value => DocumentJson.StringOf(value["culture"]) is not { } culture || cultures.Contains(culture))
            .Select(value => value.DeepClone());

        var body = new JsonObject
        {
            ["values"] = new JsonArray([.. values]),
            ["variants"] = new JsonArray([.. variants.Select(variant => (JsonNode)new JsonObject
            {
                ["culture"] = DocumentJson.StringOf(variant["culture"]),
                ["segment"] = DocumentJson.StringOf(variant["segment"]),
                ["name"] = DocumentJson.StringOf(variant["name"]),
            })]),
            ["template"] = Reference(DocumentJson.IdOf(source["template"])),
        };

        var create = row.Outcome == PlanOutcome.Create;
        if (create)
        {
            body["id"] = row.Key.ToString("D");
            body["parent"] = Reference(row.ParentKey);
            body["documentType"] = Reference(row.DocumentTypeKey ?? throw new ArgumentException($"Document {row.Key} has no document type.", nameof(row)));
        }

        var publish = row.PublishCultures.Count > 0;
        if (publish)
        {
            // Measured: invariant content publishes with an empty list ([null] fails and creates nothing).
            body["culturesToPublish"] = new JsonArray([.. row.PublishCultures.OfType<string>().Select(culture => (JsonNode)culture)]);
        }

        var path = (create, publish) switch
        {
            (true, true) => $"{RemoteRequestPolicy.ApiBase}/document/create-and-publish",
            (true, false) => $"{RemoteRequestPolicy.ApiBase}/document",
            (false, true) => $"{RemoteRequestPolicy.ApiBase}/document/{row.Key:D}/update-and-publish",
            (false, false) => $"{RemoteRequestPolicy.ApiBase}/document/{row.Key:D}",
        };

        return new DocumentWrite(create ? "POST" : "PUT", path, body.ToJsonString());
    }

    /// <summary>
    /// The write for a field-level copy. <c>PUT document/{id}</c> replaces every value (measured), so the body is the
    /// target's document with only the chosen fields taken from here: a chosen value this side lacks is cleared there,
    /// and the template and every other field stay the target's. A chosen field in a culture the target hasn't created
    /// creates that culture, with its name here.
    /// </summary>
    /// <exception cref="ArgumentException">The row is not a field-level update of a document.</exception>
    public static DocumentWrite ForFields(PlanRow row, string sourceJson, string targetJson)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind != PlanItemKind.Document || row.Fields is not { } fields || row.Outcome is not (PlanOutcome.Update or PlanOutcome.Conflict))
        {
            throw new ArgumentException($"{row.Kind} {row.Key} is {row.Outcome}{(row.Fields is null ? " as a whole node" : string.Empty)}: no fields to write.", nameof(row));
        }

        var source = DocumentJson.Parse(sourceJson, nameof(sourceJson));
        var target = DocumentJson.Parse(targetJson, nameof(targetJson));
        bool Chosen(JsonObject value) => fields.Any(field => field.CoversValue(
            DocumentJson.StringOf(value["alias"]), DocumentJson.StringOf(value["culture"]), DocumentJson.StringOf(value["segment"])));
        var values = (target["values"] as JsonArray ?? []).OfType<JsonObject>().Where(value => !Chosen(value))
            .Concat((source["values"] as JsonArray ?? []).OfType<JsonObject>().Where(Chosen))
            .Select(value => value.DeepClone());

        var sourceVariants = Created(source);
        var variants = Created(target).Select(variant => new JsonObject
        {
            ["culture"] = DocumentJson.StringOf(variant["culture"]),
            ["segment"] = DocumentJson.StringOf(variant["segment"]),
            ["name"] = DocumentJson.StringOf(fields.Any(field => field.CoversName(DocumentJson.StringOf(variant["culture"]), DocumentJson.StringOf(variant["segment"])))
                ? Matching(sourceVariants, variant)?["name"] ?? variant["name"]
                : variant["name"]),
        }).ToList();
        foreach (var variant in sourceVariants.Where(variant => Matching(variants, variant) is null))
        {
            var (culture, segment) = (DocumentJson.StringOf(variant["culture"]), DocumentJson.StringOf(variant["segment"]));
            if (fields.Any(field => string.Equals(field.Culture, culture, StringComparison.OrdinalIgnoreCase)
                && string.Equals(field.Segment, segment, StringComparison.OrdinalIgnoreCase)))
            {
                variants.Add(new JsonObject { ["culture"] = culture, ["segment"] = segment, ["name"] = DocumentJson.StringOf(variant["name"]) });
            }
        }

        var body = new JsonObject
        {
            ["values"] = new JsonArray([.. values]),
            ["variants"] = new JsonArray([.. variants]),
            ["template"] = Reference(DocumentJson.IdOf(target["template"])),
        };
        var publish = row.PublishCultures.Count > 0;
        if (publish)
        {
            body["culturesToPublish"] = new JsonArray([.. row.PublishCultures.OfType<string>().Select(culture => (JsonNode)culture)]);
        }

        var path = publish
            ? $"{RemoteRequestPolicy.ApiBase}/document/{row.Key:D}/update-and-publish"
            : $"{RemoteRequestPolicy.ApiBase}/document/{row.Key:D}";
        return new DocumentWrite("PUT", path, body.ToJsonString());
    }

    /// <summary>The sort of a parent's children on the target into this order (null parent = the root).</summary>
    public static DocumentWrite Sort(Guid? parentKey, IReadOnlyList<Guid> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var body = new JsonObject
        {
            ["parent"] = Reference(parentKey),
            ["sorting"] = new JsonArray([.. order.Select((key, index) => (JsonNode)new JsonObject { ["id"] = key.ToString("D"), ["sortOrder"] = index })]),
        };
        return new DocumentWrite("PUT", $"{RemoteRequestPolicy.ApiBase}/document/sort", body.ToJsonString());
    }

    private static JsonObject? Reference(Guid? key) => key is { } id ? new JsonObject { ["id"] = id.ToString("D") } : null;

    // A document's variants that exist (a culture not created has a variant entry too).
    private static List<JsonObject> Created(JsonObject document)
        => [.. DocumentJson.Entries(document["variants"], withAlias: false)
            .Select(variant => variant.Entry)
            .Where(variant => DocumentJson.StringOf(variant["state"]) != "NotCreated")];

    // The variant in the list with the same culture and segment.
    private static JsonObject? Matching(IEnumerable<JsonObject> variants, JsonObject variant)
        => variants.FirstOrDefault(candidate =>
            string.Equals(DocumentJson.StringOf(candidate["culture"]), DocumentJson.StringOf(variant["culture"]), StringComparison.OrdinalIgnoreCase)
            && string.Equals(DocumentJson.StringOf(candidate["segment"]), DocumentJson.StringOf(variant["segment"]), StringComparison.OrdinalIgnoreCase));
}