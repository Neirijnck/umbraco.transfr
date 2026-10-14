namespace Neirijnck.Transfr.Models;

/// <summary>
/// A node as it is on one side.
/// </summary>
public sealed record TreeSideResponseModel(string Name, bool HasChildren);