using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Everything <see cref="CopyPlanner"/> decides on, read from both sides beforehand. Documents are the Management API's
/// <c>GET document/{id}</c> JSON on either side.
/// </summary>
public sealed class CopyPlanFacts
{
    /// <summary>The selected documents here, parents before their children.</summary>
    public required IReadOnlyList<SourceDocument> Sources { get; init; }

    /// <summary>The target's copy of a source, by key (trashed ones included); no entry = not on the target.</summary>
    public required IReadOnlyDictionary<Guid, TargetDocument> TargetDocuments { get; init; }

    /// <summary>
    /// Documents on the target (not trashed) among the sources' parents and referenced documents.
    /// </summary>
    public required IReadOnlySet<Guid> DocumentsOnTarget { get; init; }

    /// <summary>The target's children of every parent a source would be created under (for namesakes).</summary>
    public required IReadOnlyList<TargetSibling> TargetSiblings { get; init; }

    /// <summary>The schema check of each source's document type, by document type key.</summary>
    public required IReadOnlyDictionary<Guid, SchemaCheck> Schema { get; init; }

    /// <summary>Referenced media items here and their ancestors, by key.</summary>
    public required IReadOnlyDictionary<Guid, LocalMediaItem> LocalMedia { get; init; }

    /// <summary>Media items on the target among the referenced ones and their ancestors.</summary>
    public required IReadOnlySet<Guid> MediaOnTarget { get; init; }

    /// <summary>The target's last update time we recorded after our last copy of a document, by key.</summary>
    public required IReadOnlyDictionary<Guid, DateTimeOffset> Baseline { get; init; }
}