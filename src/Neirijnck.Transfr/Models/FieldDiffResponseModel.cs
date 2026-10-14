namespace Neirijnck.Transfr.Models;

/// <summary>
/// A document field on both sides. Kind is one of DocumentType, Template (by name where this environment knows it, else
/// the id), Name, State (text). Differs compares the ids, not the names.
/// </summary>
public sealed record FieldDiffResponseModel(string Kind, string? Culture, string? Segment, string? Local, string? Remote, bool Differs);