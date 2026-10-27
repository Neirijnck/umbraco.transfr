using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// This environment as the side a copy reads from.
/// </summary>
internal sealed class LocalPlanSource : IPlanSource
{
    private readonly LocalTreeReader tree;
    private readonly LocalDocumentReader documents;
    private readonly LocalMediaReader media;

    public LocalPlanSource(LocalTreeReader tree, LocalDocumentReader documents, LocalMediaReader media)
    {
        this.tree = tree;
        this.documents = documents;
        this.media = media;
    }

    public int Requests => 0;

    public string Place => "here";

    public Task<IReadOnlyDictionary<Guid, LocalNode>> DescribeDocumentsAsync(IReadOnlyCollection<Guid> keys, string? culture, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.Describe(keys, culture));

    public Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, PlanItemKind kind, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.ReadLevel(parentKey, culture, kind));

    public Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        => LocalReads.EachAsync(keys, this.documents.ReadAsync);

    public Task<IReadOnlyDictionary<Guid, string>> MediaNamesAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.MediaNames(keys));

    public Task<IReadOnlyDictionary<Guid, LocalMediaNode>> MediaChainsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.MediaChains(keys));

    public Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        => LocalReads.EachAsync(keys, this.media.ReadAsync);
}