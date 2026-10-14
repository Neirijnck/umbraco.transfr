namespace Neirijnck.Transfr.Core.References;

/// <summary>
/// What a reference in content points at. Members are reported, never copied.
/// </summary>
public enum ReferenceKind
{
    Document,
    Media,
    Member,
}