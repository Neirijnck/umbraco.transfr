namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// What a plan row copies.
/// </summary>
public enum PlanItemKind
{
    Document,

    /// <summary>A media item a copied document references, or a folder it needs, planned along.</summary>
    Media,
}