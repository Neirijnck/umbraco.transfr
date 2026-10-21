namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// The schema behind one document type or media type, checked on both sides: the type, its compositions, the data types
/// of its properties and the element types those allow in blocks. Content or media of this type can be copied only when
/// it is ok.
/// </summary>
public sealed record SchemaCheck(Guid TypeKey, IReadOnlyList<SchemaProblem> Problems)
{
    public bool IsOk => this.Problems.Count == 0;
}