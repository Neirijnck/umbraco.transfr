using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Reads who last changed documents and media items on a remote: the newest content change in each one's audit log, and
/// the users' names in one batch.
/// </summary>
public sealed class RemoteAuditReader
{
    // Audit entries read per document: enough to get past a run of sorts and other non-content entries.
    private const int EntriesPerDocument = 20;

    // Keys per item/user request, as for item/document.
    private const int UsersPerRequest = 40;

    private readonly RemoteClient client;

    public RemoteAuditReader(RemoteClient client)
    {
        this.client = client;
    }

    /// <summary>The newest content change of a document or media item, or null when its recent audit log has none.</summary>
    public async Task<RemoteChange?> LastChangeAsync(RemoteDefinition remote, PlanItemKind kind, Guid key, CancellationToken cancellationToken)
    {
        var json = await this.client.GetStringAsync(
            remote,
            $"{RemoteRequestPolicy.ApiBase}/{(kind == PlanItemKind.Media ? "media" : "document")}/{key:D}/audit-log?orderDirection=Descending&skip=0&take={EntriesPerDocument}",
            cancellationToken);
        return ManagementApiAuditParser.LatestChange(json);
    }

    /// <summary>The names of these users on the remote, and the requests it cost.</summary>
    public async Task<(IReadOnlyDictionary<Guid, string> Names, int Requests)> UserNamesAsync(
        RemoteDefinition remote,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        var requests = 0;
        foreach (var batch in keys.Distinct().Chunk(UsersPerRequest))
        {
            var query = string.Join("&", batch.Select(key => $"id={key:D}"));
            var json = await this.client.GetStringAsync(remote, $"{RemoteRequestPolicy.ApiBase}/item/user?{query}", cancellationToken);
            requests++;
            foreach (var (key, name) in ManagementApiAuditParser.UserNames(json))
            {
                names[key] = name;
            }
        }

        return (names, requests);
    }
}