namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// One property of a seeded document or element type.
/// </summary>
public sealed record TestPropertySpec(string Alias, string Name, Guid DataTypeKey, bool VariesByCulture = false);