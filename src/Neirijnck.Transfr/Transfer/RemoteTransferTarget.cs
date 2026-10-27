using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Transfer;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Transfer;

/// <summary>
/// A remote as the side a transfer writes to, through its Management API (every request through the allowlist). Counts
/// every request it sends, failed ones included.
/// </summary>
internal sealed class RemoteTransferTarget : ITransferTarget
{
    private static readonly string[] ProblemFields = ["title", "detail"];

    private readonly RemoteDefinition remote;
    private readonly RemoteClient client;
    private readonly RemoteTreeReader tree;
    private readonly RemoteDocumentReader documents;
    private readonly RemoteMediaReader media;
    private int requests;

    public RemoteTransferTarget(
        RemoteDefinition remote,
        RemoteClient client,
        RemoteTreeReader tree,
        RemoteDocumentReader documents,
        RemoteMediaReader media)
    {
        this.remote = remote;
        this.client = client;
        this.tree = tree;
        this.documents = documents;
        this.media = media;
    }

    public int Requests => this.requests;

    public string Name => $"remote {this.remote.Alias}";

    public async Task<string?> SendAsync(DocumentWrite write, CancellationToken cancellationToken)
    {
        this.requests++;
        try
        {
            using var content = new StringContent(write.Body, Encoding.UTF8, "application/json");
            using var response = await this.client.SendAsync(this.remote, new HttpMethod(write.Method), write.Path, content: content, cancellationToken: cancellationToken);
            return response.IsSuccessStatusCode
                ? null
                : $"The remote answered {(int)response.StatusCode}: {ProblemOf(await response.Content.ReadAsStringAsync(cancellationToken))}";
        }
        catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
        {
            return exception.Message;
        }
    }

    public async Task<string?> UploadAsync(MediaUpload upload, Stream file, CancellationToken cancellationToken)
    {
        this.requests++;
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(upload.TemporaryFileId.ToString("D")), "Id");
            var fileContent = new StreamContent(file);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "File", upload.FileName);
            using var uploaded = await this.client.SendAsync(this.remote, HttpMethod.Post, $"{RemoteRequestPolicy.ApiBase}/temporary-file", content: form, cancellationToken: cancellationToken);
            return uploaded.IsSuccessStatusCode
                ? null
                : $"Uploading {upload.FileName} failed: the remote answered {(int)uploaded.StatusCode}: {ProblemOf(await uploaded.Content.ReadAsStringAsync(cancellationToken))}";
        }
        catch (Exception exception) when (exception is HttpRequestException or RemoteAuthenticationException)
        {
            return exception.Message;
        }
    }

    public async Task<IReadOnlyList<Guid>> ReadOrderAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken)
    {
        var (children, sent) = await this.tree.ReadLevelAsync(this.remote, parentKey, culture, cancellationToken);
        this.requests += sent;
        return [.. children.Select(child => child.Key)];
    }

    public async Task<string?> ReadAsync(PlanItemKind kind, Guid key, CancellationToken cancellationToken)
    {
        this.requests++;
        try
        {
            return kind == PlanItemKind.Media
                ? await this.media.ReadAsync(this.remote, key, cancellationToken)
                : await this.documents.ReadAsync(this.remote, key, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // The title and detail of a ProblemDetails body, or the body itself (shortened) when it isn't one.
    private static string ProblemOf(string body)
    {
        try
        {
            using var problem = JsonDocument.Parse(body);
            var parts = ProblemFields
                .Select(name => problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null)
                .Where(part => !string.IsNullOrWhiteSpace(part));
            var text = string.Join(": ", parts);
            if (text.Length > 0)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Not JSON: the body as it came, below.
        }

        return body.Length > 200 ? body[..200] + "…" : body;
    }
}