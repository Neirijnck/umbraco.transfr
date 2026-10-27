using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// The side a transfer writes to, whichever environment it is. Writes are the Management API requests
/// <see cref="TransferRequests"/> and <see cref="MediaTransferRequests"/> build; nothing else is ever sent.
/// </summary>
internal interface ITransferTarget
{
    /// <summary>Requests this side sent so far (0 for this environment).</summary>
    int Requests { get; }

    /// <summary>What to call it in a log line: the remote's alias, or "here".</summary>
    string Name { get; }

    /// <summary>Sends one create, update or sort. Returns why it failed, or null when it was written.</summary>
    Task<string?> SendAsync(DocumentWrite write, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a file available as the temporary file the media write names. Returns why it failed, or null when it was
    /// uploaded.
    /// </summary>
    Task<string?> UploadAsync(MediaUpload upload, Stream file, CancellationToken cancellationToken);

    /// <summary>The children of a document on the target (null = the root), in sort order.</summary>
    Task<IReadOnlyList<Guid>> ReadOrderAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken);

    /// <summary>
    /// The target's <c>GET document|media/{id}</c> JSON of an item, or null when it isn't there. A failed read throws.
    /// </summary>
    Task<string?> ReadAsync(PlanItemKind kind, Guid key, CancellationToken cancellationToken);
}