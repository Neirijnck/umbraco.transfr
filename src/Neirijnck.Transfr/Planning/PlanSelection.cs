using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A document picked for a copy, with or without everything below it, or only some of its fields; or a media item (a
/// folder with or without everything below it).
/// </summary>
public sealed record PlanSelection(Guid Key, bool IncludeDescendants, IReadOnlyList<FieldChoice>? Fields = null, PlanItemKind Kind = PlanItemKind.Document);