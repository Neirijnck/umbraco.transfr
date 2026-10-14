namespace Neirijnck.Transfr.Models;

/// <summary>
/// A schema item standing in the way of copying a node. Kind is DocumentType or DataType; Reason is MissingOnRemote,
/// MissingLocally or Differs; Details say what differs.
/// </summary>
public sealed record SchemaProblemResponseModel(
    string Kind,
    Guid Key,
    string? Name,
    bool IsElement,
    string Reason,
    IReadOnlyList<string> Details);