namespace Neirijnck.Transfr.Models;

/// <summary>
/// The content of a node found on both sides. Status is one of Identical, Different, Failed; Error says why for Failed.
/// </summary>
public sealed record TreeRowContentResponseModel(string Status, IReadOnlyList<ContentDifferenceResponseModel> Differences, string? Error);