namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Why a row has its outcome, or what to know about it.
/// </summary>
public enum PlanNoteKind
{
    /// <summary>Blocks: the schema behind the document type differs or is missing (see the row's schema problems).</summary>
    SchemaMismatch,

    /// <summary>Blocks: the parent is not on the target and not created by this plan.</summary>
    ParentMissingOnTarget,

    /// <summary>Blocks: the parent is created by this plan but is blocked itself.</summary>
    ParentBlocked,

    /// <summary>Blocks: a referenced document is not on the target and not created by this plan.</summary>
    MissingReference,

    /// <summary>Blocks: the content references a member; members are never copied.</summary>
    MemberReference,

    /// <summary>Blocks: under the same parent on the target, another node (other key) has the same name.</summary>
    SameNameOtherKey,

    /// <summary>Blocks: the target's node is in its recycle bin.</summary>
    TrashedOnTarget,

    /// <summary>Blocks: the target's node has another document type, which an update can't change.</summary>
    DocumentTypeDiffers,

    /// <summary>Blocks: the target's media item has another media type, which an update can't change.</summary>
    MediaTypeDiffers,

    /// <summary>Conflict: no copy of ours to go by, so the target's version may be someone's work.</summary>
    NoBaseline,

    /// <summary>Conflict: the target changed after our last copy.</summary>
    ChangedSinceLastCopy,

    /// <summary>The culture has unpublished changes on the source: its draft is copied without publishing.</summary>
    DraftOnly,

    /// <summary>
    /// The culture is published on the target but not on the source: a copy never unpublishes, so this stays.
    /// </summary>
    PublishStateNotApplied,

    /// <summary>
    /// The parent is not published on the target (nor by this plan), and a node can't be published under it: saved as
    /// a draft instead.
    /// </summary>
    ParentNotPublished,

    /// <summary>The target's node lives under another parent: a copy never moves, so it is updated where it is.</summary>
    UnderAnotherParentOnTarget,

    /// <summary>
    /// The node takes another place among its siblings on the target, the one it has on the source: right after the node
    /// in Key (null = first).
    /// </summary>
    PlacedAfter,

    /// <summary>A referenced media item doesn't exist on the source either: the reference is copied as it is.</summary>
    MediaMissingHere,

    /// <summary>Blocks: a field-level copy only updates a node, and the target doesn't have this one.</summary>
    NotOnTarget,

    /// <summary>
    /// The culture has unpublished changes on the target: the fields are saved there without publishing, so nobody's
    /// draft goes live.
    /// </summary>
    TargetHasDraft,
}