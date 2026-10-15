using System.Globalization;
using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// What a dry run saw of a document on both sides: the last save here and on the target (or that it wasn't there). A
/// transfer plans again and refuses when a row's fingerprint changed: someone saved on either side since the dry run.
/// </summary>
internal static class PlanFingerprint
{
    public static string Of(JsonObject source, JsonObject? target)
        => $"{Stamp(source)}|{(target is null ? "absent" : Stamp(target))}";

    private static string Stamp(JsonObject document)
        => DocumentJson.LastUpdate(document)?.ToString("O", CultureInfo.InvariantCulture) ?? "undated";
}