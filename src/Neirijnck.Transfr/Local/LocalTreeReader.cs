using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Tree;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// Reads this environment's document and media trees in-process, in the same shape as the remote's tree endpoints.
/// </summary>
public sealed class LocalTreeReader
{
    private const int PageSize = 500;

    private readonly IEntityService entityService;
    private readonly IIdKeyMap idKeyMap;

    public LocalTreeReader(IEntityService entityService, IIdKeyMap idKeyMap)
    {
        this.entityService = entityService;
        this.idKeyMap = idKeyMap;
    }

    /// <summary>
    /// The children of a document, or of a media folder for <see cref="PlanItemKind.Media"/> (null = the root), by sort
    /// order, without trashed items.
    /// </summary>
    public IReadOnlyList<TreeNode> ReadLevel(Guid? parentKey, string? preferredCulture, PlanItemKind kind = PlanItemKind.Document)
    {
        var objectType = ObjectTypeOf(kind);
        var nodes = new List<TreeNode>();
        long total;
        do
        {
            var page = this.entityService.GetPagedChildren(
                parentKey ?? UmbracoConstants.System.RootKey,
                objectType,
                nodes.Count,
                PageSize,
                out total,
                ordering: Ordering.By("sortOrder"));

            var before = nodes.Count;
            nodes.AddRange(page.Select((entity, index) => new TreeNode(
                entity.Key,
                NameOf(entity, preferredCulture),
                before + index,
                entity.HasChildren,
                (entity as IContentEntitySlim)?.ContentTypeKey,
                (entity as IContentEntitySlim)?.ContentTypeIcon)));
            if (nodes.Count == before)
            {
                break;
            }
        }
        while (nodes.Count < total);

        return nodes;
    }

    /// <summary>
    /// Where these documents are here: key to parent key (null = the root). Unknown and trashed keys are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid?> Locate(IReadOnlyCollection<Guid> keys, PlanItemKind kind = PlanItemKind.Document)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        var objectType = ObjectTypeOf(kind);
        return this.entityService.GetAll(objectType, keys.ToArray())
            .Where(entity => !entity.Trashed)
            .ToDictionary(entity => entity.Key, entity => this.ParentKeyOf(entity, objectType));
    }

    /// <summary>
    /// <see cref="Locate"/> for documents, plus whether each is published here in some culture.
    /// </summary>
    public IReadOnlyDictionary<Guid, ItemLocation> LocateDocuments(IReadOnlyCollection<Guid> keys)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, ItemLocation>();
        }

        return this.entityService.GetAll(UmbracoObjectTypes.Document, keys.ToArray())
            .Where(entity => !entity.Trashed)
            .ToDictionary(
                entity => entity.Key,
                entity => new ItemLocation(this.ParentKeyOf(entity, UmbracoObjectTypes.Document), entity is IDocumentEntitySlim { Published: true }));
    }

    /// <summary>
    /// Where these media items are here, keeping the trashed ones (without their recycle bin parent), as
    /// <see cref="ManagementApiTreeParser.ParseMediaItems"/> reads them on a remote. Unknown keys are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, (Guid? ParentKey, bool IsTrashed)> LocateMedia(IReadOnlyCollection<Guid> keys)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, (Guid? ParentKey, bool IsTrashed)>();
        }

        return this.entityService.GetAll(UmbracoObjectTypes.Media, keys.ToArray())
            .ToDictionary(
                entity => entity.Key,
                entity => entity.Trashed ? ((Guid?)null, true) : (this.ParentKeyOf(entity, UmbracoObjectTypes.Media), false));
    }

    private static UmbracoObjectTypes ObjectTypeOf(PlanItemKind kind) => kind == PlanItemKind.Media ? UmbracoObjectTypes.Media : UmbracoObjectTypes.Document;

    /// <summary>
    /// These documents here with their parent, name and depth. Unknown and trashed keys are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, LocalNode> Describe(IReadOnlyCollection<Guid> keys, string? preferredCulture)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, LocalNode>();
        }

        return this.entityService.GetAll(UmbracoObjectTypes.Document, keys.ToArray())
            .Where(entity => !entity.Trashed)
            .ToDictionary(
                entity => entity.Key,
                entity => new LocalNode(entity.Key, this.ParentKeyOf(entity, UmbracoObjectTypes.Document), NameOf(entity, preferredCulture), entity.Level));
    }

    /// <summary>
    /// These media items here and every folder above them, by key. Unknown and trashed items are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, LocalMediaNode> MediaChains(IReadOnlyCollection<Guid> keys)
    {
        var items = new Dictionary<Guid, LocalMediaNode>();
        var wanted = keys.ToArray();
        while (wanted.Length > 0)
        {
            var found = this.entityService.GetAll(UmbracoObjectTypes.Media, wanted)
                .Where(entity => !entity.Trashed)
                .Select(entity => new LocalMediaNode(entity.Key, this.ParentKeyOf(entity, UmbracoObjectTypes.Media), NameOf(entity, null)))
                .ToList();
            foreach (var item in found)
            {
                items[item.Key] = item;
            }

            wanted = found.Select(item => item.ParentKey).OfType<Guid>().Where(parent => !items.ContainsKey(parent)).Distinct().ToArray();
        }

        return items;
    }

    /// <summary>
    /// The names of these media items here. Unknown and trashed items are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> MediaNames(IReadOnlyCollection<Guid> keys)
        => keys.Count == 0
            ? new Dictionary<Guid, string>()
            : this.entityService.GetAll(UmbracoObjectTypes.Media, keys.ToArray())
                .Where(entity => !entity.Trashed)
                .ToDictionary(entity => entity.Key, entity => NameOf(entity, null));

    private Guid? ParentKeyOf(IEntitySlim entity, UmbracoObjectTypes objectType)
    {
        if (entity.ParentId == UmbracoConstants.System.Root)
        {
            return null;
        }

        var parent = this.idKeyMap.GetKeyForId(entity.ParentId, objectType);
        return parent.Success ? parent.Result : null;
    }

    private static string NameOf(IEntitySlim entity, string? preferredCulture)
    {
        var name = entity is IDocumentEntitySlim document
            && preferredCulture is not null
            && document.CultureNames.TryGetValue(preferredCulture, out var cultureName)
                ? cultureName
                : entity.Name;
        return string.IsNullOrWhiteSpace(name) ? ManagementApiTreeParser.UnnamedNode : name;
    }
}