namespace Neirijnck.Transfr.Models;

/// <summary>
/// One compared level of a subtree: the children of ParentKey (null = the root).
/// </summary>
public sealed record SubtreeLevelResponseModel(Guid? ParentKey, IReadOnlyList<TreeRowResponseModel> Rows);