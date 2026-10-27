using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Everything <see cref="CopyPlanner"/> decides on, read from both sides beforehand. Documents are the Management API's
/// <c>GET document/{id}</c> JSON on either side. The source is the side copied from (this environment or the remote,
/// either way); facts named Local are the source's.
/// </summary>
public sealed class CopyPlanFacts
{
    /// <summary>The selected documents on the source, parents before their children.</summary>
    public required IReadOnlyList<SourceDocument> Sources { get; init; }

    /// <summary>The target's copy of a source, by key (trashed ones included); no entry = not on the target.</summary>
    public required IReadOnlyDictionary<Guid, TargetDocument> TargetDocuments { get; init; }

    /// <summary>
    /// Documents on the target (not trashed) among the sources' parents and referenced documents.
    /// </summary>
    public required IReadOnlySet<Guid> DocumentsOnTarget { get; init; }

    /// <summary>
    /// Those of <see cref="DocumentsOnTarget"/> that are published there in some culture: a node can only be published
    /// under a published parent.
    /// </summary>
    public required IReadOnlySet<Guid> PublishedOnTarget { get; init; }

    /// <summary>
    /// The target's children of every source's parent there, in sort order: namesakes block a new node, and the order is
    /// where copied nodes are placed.
    /// </summary>
    public required IReadOnlyList<TargetSibling> TargetSiblings { get; init; }

    /// <summary>The children on the source of every source's parent, in sort order.</summary>
    public required IReadOnlyList<LocalSibling> LocalSiblings { get; init; }

    /// <summary>The schema check of each source's document type, by document type key.</summary>
    public required IReadOnlyDictionary<Guid, SchemaCheck> Schema { get; init; }

    /// <summary>
    /// Media items picked for the copy themselves (each picked folder's items included when picked with everything
    /// below it), parents before their children: they are planned whether or not a written document uses them.
    /// </summary>
    public required IReadOnlyList<Guid> PickedMedia { get; init; }

    /// <summary>Picked and referenced media items on the source and their ancestors, by key.</summary>
    public required IReadOnlyDictionary<Guid, LocalMediaItem> LocalMedia { get; init; }

    /// <summary>
    /// The target's copies of <see cref="LocalMedia"/> (trashed ones included), by key; no entry = not on the target.
    /// </summary>
    public required IReadOnlyDictionary<Guid, TargetMediaItem> TargetMedia { get; init; }

    /// <summary>The schema check of each media type in <see cref="LocalMedia"/>, by media type key.</summary>
    public required IReadOnlyDictionary<Guid, SchemaCheck> MediaSchema { get; init; }

    /// <summary>
    /// The target's last update time we recorded after our last copy of a document or media item, by key (keys are
    /// unique across documents and media).
    /// </summary>
    public required IReadOnlyDictionary<Guid, DateTimeOffset> Baseline { get; init; }
}