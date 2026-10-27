using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Core.Transfer;

/// <summary>What a write does, read back from its method and path, so this environment can carry it out itself.</summary>
public enum WriteOperation
{
    CreateDocument,
    CreateAndPublishDocument,
    UpdateDocument,
    UpdateAndPublishDocument,
    SortDocuments,
    CreateMedia,
    UpdateMedia,
}

/// <summary>A write's operation and the item it names in its path (null when the path names none).</summary>
public sealed record WriteRoute(WriteOperation Operation, Guid? Key)
{
    /// <summary>
    /// The operation of a write <see cref="TransferRequests"/> or <see cref="MediaTransferRequests"/> builds, or null
    /// for anything else: only those writes are ever carried out.
    /// </summary>
    public static WriteRoute? Parse(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        if (!path.StartsWith(RemoteRequestPolicy.ApiBase + "/", StringComparison.Ordinal))
        {
            return null;
        }

        var segments = path[(RemoteRequestPolicy.ApiBase.Length + 1)..].Split('/');
        return (method, segments) switch
        {
            ("POST", ["document"]) => new(WriteOperation.CreateDocument, null),
            ("POST", ["document", "create-and-publish"]) => new(WriteOperation.CreateAndPublishDocument, null),
            ("PUT", ["document", "sort"]) => new(WriteOperation.SortDocuments, null),
            ("PUT", ["document", var id]) when Guid.TryParseExact(id, "D", out var key) => new(WriteOperation.UpdateDocument, key),
            ("PUT", ["document", var id, "update-and-publish"]) when Guid.TryParseExact(id, "D", out var key) => new(WriteOperation.UpdateAndPublishDocument, key),
            ("POST", ["media"]) => new(WriteOperation.CreateMedia, null),
            ("PUT", ["media", var id]) when Guid.TryParseExact(id, "D", out var key) => new(WriteOperation.UpdateMedia, key),
            _ => null,
        };
    }
}