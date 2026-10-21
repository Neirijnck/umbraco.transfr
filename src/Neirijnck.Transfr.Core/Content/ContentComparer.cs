using System.Text.Json.Nodes;

namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Compares one document on both sides, each as the Management API returns it (<c>GET document/{id}</c>). Only what a
/// copy would change counts: the document type, the template, each variant's name and publish state, and each
/// property value. Dates, flags, icons and ids that are not content are never read.
/// </summary>
public static class ContentComparer
{
    /// <exception cref="System.Text.Json.JsonException">Either side is not valid JSON.</exception>
    /// <exception cref="ArgumentException">Either side is not a JSON object.</exception>
    public static ContentComparison Compare(string localJson, string remoteJson)
    {
        var local = DocumentJson.Parse(localJson, nameof(localJson));
        var remote = DocumentJson.Parse(remoteJson, nameof(remoteJson));
        var differences = new List<ContentDifference>();

        if (DocumentJson.IdOf(local["documentType"]) != DocumentJson.IdOf(remote["documentType"]))
        {
            differences.Add(new ContentDifference(ContentDifferenceKind.DocumentType));
        }

        if (DocumentJson.IdOf(local["template"]) != DocumentJson.IdOf(remote["template"]))
        {
            differences.Add(new ContentDifference(ContentDifferenceKind.Template));
        }

        CompareVariantsAndValues(local, remote, differences);
        return new ContentComparison(differences);
    }

    // Names and publish states per variant, then every value; shared with the media comparer (media variants carry no
    // state, so it never differs there).
    internal static void CompareVariantsAndValues(JsonObject local, JsonObject remote, List<ContentDifference> differences)
    {
        var localVariants = DocumentJson.Entries(local["variants"], withAlias: false);
        var remoteVariants = DocumentJson.Entries(remote["variants"], withAlias: false);
        foreach (var key in DocumentJson.UnionInOrder(localVariants.Select(v => v.Key), remoteVariants.Select(v => v.Key)))
        {
            var localVariant = localVariants.FirstOrDefault(v => v.Key == key).Entry;
            var remoteVariant = remoteVariants.FirstOrDefault(v => v.Key == key).Entry;
            var (culture, segment) = DocumentJson.CultureAndSegment(localVariant ?? remoteVariant);

            if (!string.Equals(DocumentJson.StringOf(localVariant?["name"]), DocumentJson.StringOf(remoteVariant?["name"]), StringComparison.Ordinal))
            {
                differences.Add(new ContentDifference(ContentDifferenceKind.Name, Culture: culture, Segment: segment));
            }

            if (!string.Equals(DocumentJson.StringOf(localVariant?["state"]), DocumentJson.StringOf(remoteVariant?["state"]), StringComparison.Ordinal))
            {
                differences.Add(new ContentDifference(ContentDifferenceKind.State, Culture: culture, Segment: segment));
            }
        }

        var localValues = DocumentJson.Entries(local["values"], withAlias: true);
        var remoteValues = DocumentJson.Entries(remote["values"], withAlias: true);
        foreach (var key in DocumentJson.UnionInOrder(localValues.Select(v => v.Key), remoteValues.Select(v => v.Key)))
        {
            var localValue = localValues.FirstOrDefault(v => v.Key == key).Entry;
            var remoteValue = remoteValues.FirstOrDefault(v => v.Key == key).Entry;

            var localText = DocumentJson.CanonicalText(localValue?["value"], DocumentJson.StringOf(localValue?["editorAlias"]));
            var remoteText = DocumentJson.CanonicalText(remoteValue?["value"], DocumentJson.StringOf(remoteValue?["editorAlias"]));
            if (!string.Equals(localText, remoteText, StringComparison.Ordinal))
            {
                var entry = localValue ?? remoteValue;
                var (culture, segment) = DocumentJson.CultureAndSegment(entry);
                differences.Add(new ContentDifference(ContentDifferenceKind.Value, DocumentJson.StringOf(entry?["alias"]), culture, segment));
            }
        }
    }
}