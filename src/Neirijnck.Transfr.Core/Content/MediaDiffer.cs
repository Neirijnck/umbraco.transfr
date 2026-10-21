namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Lays one media item out on both sides for a diff view: its media type and name, and every property value (a file
/// value as its path, focal point and crops). Whether something differs is decided exactly as <see cref="MediaComparer"/>
/// decides it.
/// </summary>
public static class MediaDiffer
{
    /// <exception cref="System.Text.Json.JsonException">Either side is not valid JSON.</exception>
    /// <exception cref="ArgumentException">Either side is not a JSON object.</exception>
    public static DocumentDiff Diff(string localJson, string remoteJson)
    {
        var local = DocumentJson.Parse(localJson, nameof(localJson));
        var remote = DocumentJson.Parse(remoteJson, nameof(remoteJson));

        var fields = new List<FieldDiff>
        {
            new(ContentDifferenceKind.MediaType, null, null, DocumentJson.IdOf(local["mediaType"])?.ToString("D"), DocumentJson.IdOf(remote["mediaType"])?.ToString("D")),
        };

        // Media variants carry a name, no publish state.
        var localVariants = DocumentJson.Entries(local["variants"], withAlias: false);
        var remoteVariants = DocumentJson.Entries(remote["variants"], withAlias: false);
        foreach (var key in DocumentJson.UnionInOrder(localVariants.Select(v => v.Key), remoteVariants.Select(v => v.Key)))
        {
            var localVariant = localVariants.FirstOrDefault(v => v.Key == key).Entry;
            var remoteVariant = remoteVariants.FirstOrDefault(v => v.Key == key).Entry;
            var (culture, segment) = DocumentJson.CultureAndSegment(localVariant ?? remoteVariant);
            fields.Add(new(ContentDifferenceKind.Name, culture, segment, DocumentJson.StringOf(localVariant?["name"]), DocumentJson.StringOf(remoteVariant?["name"])));
        }

        return new DocumentDiff(fields, DocumentDiffer.DiffValues(local["values"], remote["values"]));
    }
}