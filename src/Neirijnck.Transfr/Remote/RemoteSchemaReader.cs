using System.Net;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Reads schema items from a remote through its Management API. The remote's API user can read these by key without
/// the Settings section (measured); the schema trees themselves are closed to it and never needed.
/// </summary>
public sealed class RemoteSchemaReader
{
    private readonly RemoteClient client;

    public RemoteSchemaReader(RemoteClient client)
    {
        this.client = client;
    }

    /// <summary>
    /// The item's JSON, or null when the remote doesn't have it (404). Other failures throw.
    /// </summary>
    public async Task<string?> ReadAsync(RemoteDefinition remote, SchemaItemKind kind, Guid key, CancellationToken cancellationToken)
    {
        var path = kind switch
        {
            SchemaItemKind.DocumentType => $"{RemoteRequestPolicy.ApiBase}/document-type/{key:D}",
            SchemaItemKind.DataType => $"{RemoteRequestPolicy.ApiBase}/data-type/{key:D}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        try
        {
            return await this.client.GetStringAsync(remote, path, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// The remote's document types whose name matches the query.
    /// </summary>
    public async Task<IReadOnlyList<(Guid Key, string Name)>> SearchDocumentTypesAsync(RemoteDefinition remote, string query, CancellationToken cancellationToken)
    {
        var json = await this.client.GetStringAsync(
            remote,
            $"{RemoteRequestPolicy.ApiBase}/item/document-type/search?query={Uri.EscapeDataString(query)}&skip=0&take=20",
            cancellationToken);
        return ManagementApiSchemaParser.ParseDocumentTypeSearch(json);
    }
}