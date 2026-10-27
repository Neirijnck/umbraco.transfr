using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Tree;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// The side a copy writes to: what <see cref="CopyPlanService"/> reads of it, whichever environment it is.
/// </summary>
internal interface IPlanTarget
{
    /// <summary>Requests this side sent so far (0 for this environment).</summary>
    int Requests { get; }

    /// <summary>
    /// The <c>GET document/{id}</c> JSON of the target's copy of these documents (trashed ones included). Keys it doesn't
    /// have are left out; any other failure fails the plan.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>Where these documents are on the target, and whether each is published there. Unknown and trashed keys are left out.</summary>
    Task<IReadOnlyDictionary<Guid, ItemLocation>> LocateDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>The children of a document on the target (null = the root), by sort order.</summary>
    Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken);

    /// <summary>Where these media items are on the target, trashed ones included. Unknown keys are left out.</summary>
    Task<IReadOnlyDictionary<Guid, (Guid? ParentKey, bool IsTrashed)>> LocateMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>
    /// The <c>GET media/{id}</c> JSON of these media items on the target. Keys it doesn't have are left out; any other
    /// failure fails the plan.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken);

    /// <summary>
    /// Who last changed each of these documents and media items on the target, and when. Only an explanation: what can't
    /// be read is left out, never failing the plan.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RemoteChangeInfo>> LastChangesAsync(IReadOnlyList<(PlanItemKind Kind, Guid Key)> keys, CancellationToken cancellationToken);
}