using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>
/// What a transfer reads back from a document it wrote (<c>GET document/{id}</c>): when it was last saved, for the copy
/// history, and its publish state per culture, for the report.
/// </summary>
public static class DocumentStamp
{
    /// <summary>The last save of any variant (null when no variant carries a date).</summary>
    /// <exception cref="System.Text.Json.JsonException">Not valid JSON.</exception>
    public static DateTimeOffset? LastUpdate(string documentJson)
        => DocumentJson.LastUpdate(DocumentJson.Parse(documentJson, nameof(documentJson)));

    /// <summary>E.g. "Published" for invariant content, "en-US Published, nl-BE Draft" for variants.</summary>
    /// <exception cref="System.Text.Json.JsonException">Not valid JSON.</exception>
    public static string States(string documentJson)
        => string.Join(", ", DocumentJson.Entries(DocumentJson.Parse(documentJson, nameof(documentJson))["variants"], withAlias: false)
            .Select(variant => (Culture: DocumentJson.StringOf(variant.Entry["culture"]), State: DocumentJson.StringOf(variant.Entry["state"]) ?? "unknown"))
            .Select(variant => variant.Culture is null ? variant.State : $"{variant.Culture} {variant.State}"));
}