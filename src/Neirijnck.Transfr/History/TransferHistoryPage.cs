using Neirijnck.Transfr.Core.History;

namespace Neirijnck.Transfr.History;

/// <summary>
/// A page of transfers to a remote, newest first, with the names the view needs.
/// </summary>
/// <param name="Transfers">The transfers on this page.</param>
/// <param name="NextBefore">The row id to read the next page before (null when this is the last page).</param>
/// <param name="UserNames">Names of the users here who ran them (a deleted user is missing).</param>
/// <param name="DocumentNames">Names of the documents and media items here (one no longer here is missing).</param>
public sealed record TransferHistoryPage(
    IReadOnlyList<TransferRecord> Transfers,
    int? NextBefore,
    IReadOnlyDictionary<Guid, string> UserNames,
    IReadOnlyDictionary<Guid, string> DocumentNames);