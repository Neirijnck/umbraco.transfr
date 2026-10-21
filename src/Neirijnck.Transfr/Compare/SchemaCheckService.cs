using System.Collections.Concurrent;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Schema;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// Checks the schema behind document types and media types against a remote with <see cref="SchemaChecker"/>, and tells
/// a separately installed remote (the same alias under another key) from one that just misses a type. Remote schema items
/// are kept for a few minutes per remote site, so expanding level after level doesn't read the same types again.
/// </summary>
public sealed class SchemaCheckService
{
    // Schema changes far less often than content; a refresh after this picks up a deployment.
    private static readonly TimeSpan RemoteCacheLifetime = TimeSpan.FromMinutes(5);

    private readonly LocalSchemaReader localSchema;
    private readonly RemoteSchemaReader remoteSchema;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, (DateTimeOffset ReadAt, string? Json)> remoteItems = new(StringComparer.Ordinal);

    public SchemaCheckService(LocalSchemaReader localSchema, RemoteSchemaReader remoteSchema, TimeProvider timeProvider)
    {
        this.localSchema = localSchema;
        this.remoteSchema = remoteSchema;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Checks each document type, and looks for evidence of a separate installation among the ones missing on the remote.
    /// </summary>
    public async Task<SchemaLevelCheck> CheckAsync(RemoteDefinition remote, IReadOnlyCollection<Guid> documentTypeKeys, CancellationToken cancellationToken)
    {
        var requests = 0;
        var checks = new Dictionary<Guid, SchemaCheck>();
        foreach (var key in documentTypeKeys)
        {
            checks[key] = await SchemaChecker.CheckAsync(
                key,
                (kind, itemKey, _) => this.localSchema.ReadAsync(kind, itemKey),
                async (kind, itemKey, token) =>
                {
                    var (json, read) = await this.ReadRemoteAsync(remote, kind, itemKey, token);
                    requests += read ? 1 : 0;
                    return json;
                },
                cancellationToken);
        }

        var separateInstall = new List<SeparateInstallEvidence>();
        var missing = checks.Values
            .SelectMany(check => check.Problems)
            .Where(problem => problem.Kind == SchemaItemKind.DocumentType && problem.Reason == SchemaProblemReason.MissingOnRemote)
            .DistinctBy(problem => problem.Key);
        foreach (var problem in missing)
        {
            var localJson = await this.localSchema.ReadAsync(SchemaItemKind.DocumentType, problem.Key);
            var alias = localJson is null ? null : ManagementApiSchemaParser.ParseAlias(localJson);
            if (alias is null || problem.Name is null)
            {
                continue;
            }

            // The search matches names; only the same alias under another key proves a separate installation.
            requests++;
            foreach (var (candidate, _) in await this.remoteSchema.SearchDocumentTypesAsync(remote, problem.Name, cancellationToken))
            {
                var (json, read) = await this.ReadRemoteAsync(remote, SchemaItemKind.DocumentType, candidate, cancellationToken);
                requests += read ? 1 : 0;
                if (candidate != problem.Key
                    && json is not null
                    && string.Equals(ManagementApiSchemaParser.ParseAlias(json), alias, StringComparison.OrdinalIgnoreCase))
                {
                    separateInstall.Add(new SeparateInstallEvidence(alias, problem.Name, problem.Key, candidate));
                    break;
                }
            }
        }

        return new SchemaLevelCheck(checks, separateInstall, requests);
    }

    /// <summary>
    /// Checks each media type. A separate installation shows in the document types; built-in media types share their
    /// keys across installations (measured), so their check needs no evidence of its own.
    /// </summary>
    public async Task<(IReadOnlyDictionary<Guid, SchemaCheck> Checks, int RemoteRequests)> CheckMediaTypesAsync(
        RemoteDefinition remote,
        IReadOnlyCollection<Guid> mediaTypeKeys,
        CancellationToken cancellationToken)
    {
        var requests = 0;
        var checks = new Dictionary<Guid, SchemaCheck>();
        foreach (var key in mediaTypeKeys)
        {
            checks[key] = await SchemaChecker.CheckMediaTypeAsync(
                key,
                (kind, itemKey, _) => this.localSchema.ReadAsync(kind, itemKey),
                async (kind, itemKey, token) =>
                {
                    var (json, read) = await this.ReadRemoteAsync(remote, kind, itemKey, token);
                    requests += read ? 1 : 0;
                    return json;
                },
                cancellationToken);
        }

        return (checks, requests);
    }

    // A remote item from the cache while fresh, else read; Read says whether a request was sent.
    private async Task<(string? Json, bool Read)> ReadRemoteAsync(RemoteDefinition remote, SchemaItemKind kind, Guid key, CancellationToken cancellationToken)
    {
        var cacheKey = $"{remote.BaseUrl}|{kind}|{key:D}";
        var now = this.timeProvider.GetUtcNow();
        if (this.remoteItems.TryGetValue(cacheKey, out var cached) && now - cached.ReadAt < RemoteCacheLifetime)
        {
            return (cached.Json, false);
        }

        var json = await this.remoteSchema.ReadAsync(remote, kind, key, cancellationToken);
        this.remoteItems[cacheKey] = (now, json);
        return (json, true);
    }
}