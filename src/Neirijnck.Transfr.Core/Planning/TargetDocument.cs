namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// The target's copy of a source document.
/// </summary>
/// <param name="ParentKey">Its parent on the target (null = root).</param>
/// <param name="Json">Its <c>GET document/{id}</c> JSON.</param>
public sealed record TargetDocument(Guid? ParentKey, string Json);