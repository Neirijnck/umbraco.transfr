using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Models;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A <see cref="CopyPlanResult"/> as the API answers it, with the names looked up for its keys and aliases.
/// </summary>
internal static class PlanResponses
{
    public static CopyPlanResponseModel From(CopyPlanResult result)
        => new(
            result.Rows.Select(row => Row(row, result)).ToList(),
            result.SeparateInstall.DistinctBy(evidence => evidence.LocalKey).Select(ResponseMapping.From).ToList(),
            result.RemoteRequests);

    private static PlanRowResponseModel Row(PlanRow row, CopyPlanResult result)
    {
        var type = row.DocumentTypeKey is { } key ? result.DocumentTypes.GetValueOrDefault(key) : null;
        return new PlanRowResponseModel(
            row.Kind.ToString(),
            row.Key,
            row.ParentKey,
            row.Name,
            type?.Name,
            row.Outcome.ToString(),
            row.Notes.Select(note => Note(note, type, result)).ToList(),
            row.PublishCultures,
            row.Differences.Select(ResponseMapping.From).ToList(),
            row.SchemaProblems.Select(ResponseMapping.From).ToList());
    }

    private static PlanNoteResponseModel Note(PlanNote note, ContentTypeNames? type, CopyPlanResult result)
        => new(
            note.Kind.ToString(),
            note.Blocks,
            note.Key,
            note.Key is { } key ? result.Names.GetValueOrDefault(key) : null,
            note.PropertyAlias,
            note.PropertyAlias is { } alias ? type?.PropertyLabels.GetValueOrDefault(alias) : null,
            note.Culture,
            note.Kind is PlanNoteKind.MissingReference or PlanNoteKind.ParentMissingOnTarget
                && note.Key is { } selectable
                && result.SelectableHere.Contains(selectable));
}