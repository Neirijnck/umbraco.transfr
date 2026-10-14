using Neirijnck.Transfr.Core.Tree;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Local;

/// <summary>
/// Reads this environment's document tree in-process, in the same shape as the remote's tree endpoints.
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
    /// The children of a document (null = the root), by sort order, without trashed documents.
    /// </summary>
    public IReadOnlyList<TreeNode> ReadLevel(Guid? parentKey, string? preferredCulture)
    {
        var nodes = new List<TreeNode>();
        long total;
        do
        {
            var page = this.entityService.GetPagedChildren(
                parentKey ?? UmbracoConstants.System.RootKey,
                UmbracoObjectTypes.Document,
                nodes.Count,
                PageSize,
                out total,
                ordering: Ordering.By("sortOrder"));

            var before = nodes.Count;
            nodes.AddRange(page.Select((entity, index) => new TreeNode(entity.Key, NameOf(entity, preferredCulture), before + index, entity.HasChildren)));
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
    public IReadOnlyDictionary<Guid, Guid?> Locate(IReadOnlyCollection<Guid> keys)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        return this.entityService.GetAll(UmbracoObjectTypes.Document, keys.ToArray())
            .Where(entity => !entity.Trashed)
            .ToDictionary(entity => entity.Key, entity => this.ParentKeyOf(entity));
    }

    private Guid? ParentKeyOf(IEntitySlim entity)
    {
        if (entity.ParentId == UmbracoConstants.System.Root)
        {
            return null;
        }

        var parent = this.idKeyMap.GetKeyForId(entity.ParentId, UmbracoObjectTypes.Document);
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