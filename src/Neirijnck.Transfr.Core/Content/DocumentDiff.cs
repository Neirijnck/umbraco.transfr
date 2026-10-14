namespace Neirijnck.Transfr.Core.Content;

/// <summary>
/// One document on both sides, field by field, for showing what differs. <see cref="ContentComparer"/> gives the
/// verdict; this gives the detail, from the same reading of the document.
/// </summary>
/// <param name="Fields">The document type, the template, and each variant's name and publish state, differing or not.</param>
/// <param name="Properties">Every property value on either side, the local ones in their order first.</param>
public sealed record DocumentDiff(IReadOnlyList<FieldDiff> Fields, IReadOnlyList<PropertyDiff> Properties)
{
    public bool IsIdentical => this.Fields.All(diff => !diff.Differs) && this.Properties.All(property => !property.Value.Differs);
}