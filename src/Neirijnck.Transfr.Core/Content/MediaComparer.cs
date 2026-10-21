namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// Compares one media item on both sides, each as the Management API returns it (<c>GET media/{id}</c>): the media
/// type, each variant's name and each property value. The file itself is not read: a file is compared through its
/// value (its path, which follows from the media key and the file name, and the size, dimensions and extension Umbraco
/// fills in on upload), so a file replaced by another with the same name, size and dimensions counts as identical.
/// </summary>
public static class MediaComparer
{
    /// <exception cref="System.Text.Json.JsonException">Either side is not valid JSON.</exception>
    /// <exception cref="ArgumentException">Either side is not a JSON object.</exception>
    public static ContentComparison Compare(string localJson, string remoteJson)
    {
        var local = DocumentJson.Parse(localJson, nameof(localJson));
        var remote = DocumentJson.Parse(remoteJson, nameof(remoteJson));
        var differences = new List<ContentDifference>();

        if (DocumentJson.IdOf(local["mediaType"]) != DocumentJson.IdOf(remote["mediaType"]))
        {
            differences.Add(new ContentDifference(ContentDifferenceKind.MediaType));
        }

        ContentComparer.CompareVariantsAndValues(local, remote, differences);
        return new ContentComparison(differences);
    }
}