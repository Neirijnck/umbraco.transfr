using System.Net;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Reads one document on both sides, lays it out for the diff view with <see cref="DocumentDiffer"/>, and looks up the
/// names behind its keys and aliases in this environment's schema.
/// </summary>
public sealed class DocumentDiffService
{
    private readonly LocalDocumentReader localDocuments;
    private readonly RemoteDocumentReader remoteDocuments;
    private readonly LocalSchemaNames schemaNames;

    public DocumentDiffService(LocalDocumentReader localDocuments, RemoteDocumentReader remoteDocuments, LocalSchemaNames schemaNames)
    {
        this.localDocuments = localDocuments;
        this.remoteDocuments = remoteDocuments;
        this.schemaNames = schemaNames;
    }

    /// <summary>
    /// The diff with its names, or null when the document is missing on either side. Other remote failures throw.
    /// </summary>
    public async Task<(DocumentDiff Diff, SchemaNames Names)?> DiffAsync(RemoteDefinition remote, Guid key, CancellationToken cancellationToken)
    {
        var local = await this.localDocuments.ReadAsync(key);
        if (local is null)
        {
            return null;
        }

        string remoteJson;
        try
        {
            remoteJson = await this.remoteDocuments.ReadAsync(remote, key, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Not a failure: the document simply isn't on the remote; the controller answers 404.
            return null;
        }

        var diff = DocumentDiffer.Diff(local, remoteJson);
        return (diff, await this.schemaNames.ForAsync(diff));
    }
}