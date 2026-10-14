namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// What a plan row copies.
/// </summary>
public enum PlanItemKind
{
    Document,

    /// <summary>A media item (or folder) a copied document needs, planned along.</summary>
    Media,
}