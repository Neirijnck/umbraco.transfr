using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// The side a copy reads from: what <see cref="CopyPlanService"/> needs of it, whichever environment it is. Every read
/// skips trashed items.
/// </summary>
internal interface IPlanSource
{
    /// <summary>Requests this side sent so far (0 for this environment).</summary>
    int Requests { get; }

    /// <summary>Where it is, to name in a message: "here" or "on" and the remote's name.</summary>
    string Place { get; }

    /// <summary>
    /// These documents with their parent, name and depth (0 when the side doesn't tell). Unknown keys are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, LocalNode>> DescribeDocumentsAsync(IReadOnlyCollection<Guid> keys, string? culture, CancellationToken cancellationToken);

    /// <summary>The children of a document, or of a media folder (null = the root), by sort order.</summary>
    Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, PlanItemKind kind, CancellationToken cancellationToken);

    /// <summary>The <c>GET document/{id}</c> JSON of these documents. Unreadable ones are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken);

    /// <summary>The names of these media items. Unknown keys are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> MediaNamesAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>These media items and every folder above them, by key. Unknown keys are left out.</summary>
    Task<IReadOnlyDictionary<Guid, LocalMediaNode>> MediaChainsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>The <c>GET media/{id}</c> JSON of these media items. Unreadable ones are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken);
}