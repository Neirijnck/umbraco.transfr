namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// A selected document here.
/// </summary>
/// <param name="Key">Its key.</param>
/// <param name="ParentKey">Its parent (null = root).</param>
/// <param name="Name">Its name (in the default culture for variant content).</param>
/// <param name="Json">Its <c>GET document/{id}</c> JSON.</param>
/// <param name="Fields">For a field-level copy: the fields to carry over; null = the whole node.</param>
public sealed record SourceDocument(Guid Key, Guid? ParentKey, string Name, string Json, IReadOnlyList<FieldChoice>? Fields = null);