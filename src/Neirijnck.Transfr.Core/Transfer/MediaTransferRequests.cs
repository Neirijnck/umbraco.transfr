using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// The requests that carry out a media row on the target, built from the media item as <c>GET media/{id}</c> returns it
/// on the source. Measured in 17.7.0 with the API user: a file goes up as a temporary file and the media value names it
/// (<c>temporaryFileId</c>); a value with only <c>src</c> keeps the file; <c>PUT media/{id}</c> replaces every value (one
/// left out is cleared, and a file left out stays on disk), so every value is always sent.
/// </summary>
public static class MediaTransferRequests
{
    // Umbraco fills these in from the file on upload (posted values are overwritten, measured), so a difference in them
    // means another file even when its name, and so its path, is the same.
    private const string LabelEditor = "Umbraco.Label";

    /// <summary>
    /// The create (key, parent and media type kept) or the update of a media item, uploading a file only when it is new
    /// there or differs: another path, or other labels Umbraco derives from the file (size, dimensions). An unchanged
    /// file is named by its path, which is the same on both sides (it follows from the media key, the property and the
    /// file name).
    /// </summary>
    /// <param name="row">A media row to create, update, or overwrite as a conflict.</param>
    /// <param name="sourceJson">The media item on the source.</param>
    /// <param name="targetJson">The target's media item, for an update or a conflict.</param>
    /// <param name="newId">Gives each upload its temporary file id.</param>
    /// <exception cref="ArgumentException">
    /// The row is not a media item to write, or an update comes without the target's media item.
    /// </exception>
    public static MediaWrite For(PlanRow row, string sourceJson, string? targetJson, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(newId);
        if (row.Kind != PlanItemKind.Media || row.Outcome is not (PlanOutcome.Create or PlanOutcome.Update or PlanOutcome.Conflict))
        {
            throw new ArgumentException($"{row.Kind} {row.Key} is {row.Outcome}: no media to write.", nameof(row));
        }

        var create = row.Outcome == PlanOutcome.Create;
        if (!create && targetJson is null)
        {
            throw new ArgumentException($"Media {row.Key} is updated without the target's media item.", nameof(targetJson));
        }

        var source = DocumentJson.Parse(sourceJson, nameof(sourceJson));
        var target = targetJson is null ? null : DocumentJson.Parse(targetJson, nameof(targetJson));
        var targetValues = DocumentJson.Entries(target?["values"], withAlias: true);
        var labelsDiffer = create || DocumentJson.Entries(source["values"], withAlias: true)
            .Where(value => DocumentJson.StringOf(value.Entry["editorAlias"]) == LabelEditor)
            .Any(value => !string.Equals(
                DocumentJson.CanonicalText(value.Entry["value"], LabelEditor),
                DocumentJson.CanonicalText(targetValues.FirstOrDefault(entry => entry.Key == value.Key).Entry?["value"], LabelEditor),
                StringComparison.Ordinal));

        var uploads = new List<MediaUpload>();
        var values = new JsonArray();
        foreach (var (key, value) in DocumentJson.Entries(source["values"], withAlias: true))
        {
            var copy = (JsonObject)value.DeepClone();
            // A file value is { src, ... } on the source, { temporaryFileId, ... } to upload a new one.
            var src = MediaFiles.IsFile(value) ? MediaFiles.SrcOf(value) : null;
            var targetSrc = MediaFiles.SrcOf(targetValues.FirstOrDefault(entry => entry.Key == key).Entry);

            // An empty file on the source is sent as it is: that clears the file there too.
            if (!string.IsNullOrEmpty(src) && (labelsDiffer || !string.Equals(src, targetSrc, StringComparison.Ordinal)))
            {
                var upload = new MediaUpload(newId(), src, Uri.UnescapeDataString(src[(src.LastIndexOf('/') + 1)..]));
                uploads.Add(upload);
                var fileValue = (JsonObject)copy["value"]!;
                fileValue.Remove("src");
                fileValue["temporaryFileId"] = upload.TemporaryFileId.ToString("D");
            }

            values.Add(copy);
        }

        var body = new JsonObject
        {
            ["values"] = values,
            ["variants"] = new JsonArray([.. DocumentJson.Entries(source["variants"], withAlias: false).Select(variant => (JsonNode)new JsonObject
            {
                ["culture"] = DocumentJson.StringOf(variant.Entry["culture"]),
                ["segment"] = DocumentJson.StringOf(variant.Entry["segment"]),
                ["name"] = DocumentJson.StringOf(variant.Entry["name"]),
            })]),
        };

        if (create)
        {
            body["id"] = row.Key.ToString("D");
            body["parent"] = row.ParentKey is { } parentKey ? new JsonObject { ["id"] = parentKey.ToString("D") } : null;
            body["mediaType"] = new JsonObject
            {
                ["id"] = (DocumentJson.IdOf(source["mediaType"]) ?? throw new ArgumentException($"Media {row.Key} names no media type.", nameof(sourceJson))).ToString("D"),
            };
        }

        var write = create
            ? new DocumentWrite("POST", $"{RemoteRequestPolicy.ApiBase}/media", body.ToJsonString())
            : new DocumentWrite("PUT", $"{RemoteRequestPolicy.ApiBase}/media/{row.Key:D}", body.ToJsonString());
        return new MediaWrite(uploads, write);
    }

    /// <summary>
    /// The file values that didn't arrive: a file on the source whose value on the target, read back after the write, has
    /// no path. Umbraco saves an uploaded file it refuses (a disallowed extension, an unsafe file) as an empty path and
    /// still answers with success, so only reading back tells.
    /// </summary>
    /// <returns>The aliases of the file values that came out empty there; empty when every file arrived.</returns>
    public static IReadOnlyList<string> MissingFiles(string sourceJson, string writtenJson)
    {
        var source = DocumentJson.Parse(sourceJson, nameof(sourceJson));
        var written = DocumentJson.Entries(DocumentJson.Parse(writtenJson, nameof(writtenJson))["values"], withAlias: true);
        return [.. DocumentJson.Entries(source["values"], withAlias: true)
            .Where(value => MediaFiles.IsFile(value.Entry)
                && !string.IsNullOrEmpty(MediaFiles.SrcOf(value.Entry))
                && string.IsNullOrEmpty(MediaFiles.SrcOf(written.FirstOrDefault(entry => entry.Key == value.Key).Entry)))
            .Select(value => DocumentJson.StringOf(value.Entry["alias"]) ?? string.Empty)];
    }
}