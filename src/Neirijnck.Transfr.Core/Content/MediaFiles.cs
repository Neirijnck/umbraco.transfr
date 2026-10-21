using System.Text.Json.Nodes;

namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// The files in a media item as <c>GET media/{id}</c> returns it: the values of the editors that hold a file (image
/// cropper, upload field), each <c>{ src, ... }</c>.
/// </summary>
public static class MediaFiles
{
    private static readonly HashSet<string> FileEditors = new(StringComparer.Ordinal) { "Umbraco.ImageCropper", "Umbraco.UploadField" };

    /// <summary>Every file value with a path, in the order of the values.</summary>
    /// <exception cref="System.Text.Json.JsonException">Not valid JSON.</exception>
    public static IReadOnlyList<MediaFile> Of(string mediaJson)
        => [.. DocumentJson.Entries(DocumentJson.Parse(mediaJson, nameof(mediaJson))["values"], withAlias: true)
            .Select(value => value.Entry)
            .Where(IsFile)
            .Select(value => (Value: value, Src: SrcOf(value)))
            .Where(file => !string.IsNullOrEmpty(file.Src))
            .Select(file => new MediaFile(
                DocumentJson.StringOf(file.Value["alias"]) ?? string.Empty,
                DocumentJson.StringOf(file.Value["culture"]),
                DocumentJson.StringOf(file.Value["segment"]),
                file.Src!))];

    /// <summary>Whether a value entry belongs to an editor that holds a file.</summary>
    internal static bool IsFile(JsonObject value) => FileEditors.Contains(DocumentJson.StringOf(value["editorAlias"]) ?? string.Empty);

    /// <summary>The path in a file value (null for a value that isn't one, or has none).</summary>
    internal static string? SrcOf(JsonObject? value) => value?["value"] is JsonObject file ? DocumentJson.StringOf(file["src"]) : null;
}