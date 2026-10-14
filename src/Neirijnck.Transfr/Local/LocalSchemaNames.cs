using Neirijnck.Transfr.Core.Content;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// The names behind the keys and aliases in a diff, from this environment's schema: document and element type names,
/// their property labels (compositions included) and template names. What this environment doesn't have is left out;
/// the view then shows the alias or id.
/// </summary>
public sealed class LocalSchemaNames
{
    private readonly IContentTypeService contentTypeService;
    private readonly ITemplateService templateService;

    public LocalSchemaNames(IContentTypeService contentTypeService, ITemplateService templateService)
    {
        this.contentTypeService = contentTypeService;
        this.templateService = templateService;
    }

    public async Task<SchemaNames> ForAsync(DocumentDiff diff)
    {
        var typeKeys = new HashSet<Guid>();
        foreach (var field in diff.Fields.Where(field => field.Kind == ContentDifferenceKind.DocumentType))
        {
            AddKey(typeKeys, field.Local);
            AddKey(typeKeys, field.Remote);
        }

        CollectBlockTypes(diff.Properties, typeKeys);

        var types = new Dictionary<Guid, ContentTypeNames>();
        foreach (var key in typeKeys)
        {
            var type = this.contentTypeService.Get(key);
            if (type is not null)
            {
                var labels = type.CompositionPropertyTypes
                    .GroupBy(property => property.Alias, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
                types[key] = new ContentTypeNames(type.Name ?? type.Alias, labels);
            }
        }

        var templates = new Dictionary<Guid, string>();
        foreach (var field in diff.Fields.Where(field => field.Kind == ContentDifferenceKind.Template))
        {
            foreach (var id in new[] { field.Local, field.Remote })
            {
                if (Guid.TryParse(id, out var key) && !templates.ContainsKey(key) && await this.templateService.GetAsync(key) is { } template)
                {
                    templates[key] = template.Name ?? template.Alias;
                }
            }
        }

        return new SchemaNames(types, templates);
    }

    /// <summary>
    /// The names of these document types here; keys this environment doesn't have are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> DocumentTypeNames(IEnumerable<Guid> keys)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var key in keys.Distinct())
        {
            if (this.contentTypeService.Get(key) is { } type)
            {
                names[key] = type.Name ?? type.Alias;
            }
        }

        return names;
    }

    private static void AddKey(HashSet<Guid> keys, string? id)
    {
        if (Guid.TryParse(id, out var key))
        {
            keys.Add(key);
        }
    }

    private static void CollectBlockTypes(IEnumerable<PropertyDiff> properties, HashSet<Guid> keys)
    {
        foreach (var block in properties.SelectMany(property => property.Value.Blocks))
        {
            if (block.ContentTypeKey is { } contentType)
            {
                keys.Add(contentType);
            }

            if (block.SettingsTypeKey is { } settingsType)
            {
                keys.Add(settingsType);
            }

            CollectBlockTypes(block.Values.Concat(block.Settings), keys);
        }
    }
}