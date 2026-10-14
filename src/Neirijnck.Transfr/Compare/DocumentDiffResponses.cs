using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Models;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Turns a diff into its response, with names where this environment's schema has them: a property's label, a block's
/// element type, the document type and template by name. Anything unknown falls back to its alias or id.
/// </summary>
internal static class DocumentDiffResponses
{
    public static DocumentDiffResponseModel From(DocumentDiff diff, SchemaNames names)
    {
        var documentTypeField = diff.Fields.First(field => field.Kind == ContentDifferenceKind.DocumentType);
        var documentType = TypeOf(documentTypeField.Local ?? documentTypeField.Remote, names);

        var fields = diff.Fields
            .Select(field => new FieldDiffResponseModel(
                field.Kind.ToString(),
                field.Culture,
                field.Segment,
                Display(field.Kind, field.Local, names),
                Display(field.Kind, field.Remote, names),
                field.Differs))
            .ToList();

        return new DocumentDiffResponseModel(diff.IsIdentical, documentType?.Name, fields, Properties(diff.Properties, documentType, names));
    }

    private static string? Display(ContentDifferenceKind kind, string? id, SchemaNames names)
        => kind switch
        {
            ContentDifferenceKind.DocumentType => TypeOf(id, names)?.Name ?? id,
            ContentDifferenceKind.Template => Guid.TryParse(id, out var key) && names.Templates.TryGetValue(key, out var name) ? name : id,
            _ => id,
        };

    private static ContentTypeNames? TypeOf(string? id, SchemaNames names)
        => Guid.TryParse(id, out var key) ? TypeOf(key, names) : null;

    private static ContentTypeNames? TypeOf(Guid? key, SchemaNames names)
        => key is { } typeKey ? names.Types.GetValueOrDefault(typeKey) : null;

    private static List<PropertyDiffResponseModel> Properties(IEnumerable<PropertyDiff> properties, ContentTypeNames? type, SchemaNames names)
        => properties
            .Select(property => new PropertyDiffResponseModel(
                property.Alias,
                type?.PropertyLabels.GetValueOrDefault(property.Alias) ?? property.Alias,
                property.Culture,
                property.Segment,
                property.EditorAlias,
                new ValueDiffResponseModel(
                    property.Value.Differs,
                    property.Value.Local,
                    property.Value.Remote,
                    property.Value.Blocks.Select(block => Block(block, names)).ToList())))
            .ToList();

    private static BlockDiffResponseModel Block(BlockDiff block, SchemaNames names)
    {
        var content = TypeOf(block.ContentTypeKey, names);
        return new BlockDiffResponseModel(
            block.Key,
            block.Status.ToString(),
            block.ContentTypeKey,
            content?.Name,
            block.LocalIndex,
            block.RemoteIndex,
            Properties(block.Values, content, names),
            Properties(block.Settings, TypeOf(block.SettingsTypeKey, names), names));
    }
}