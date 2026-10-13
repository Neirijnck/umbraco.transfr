using System.Collections.Concurrent;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// Remotes connected at runtime, per backoffice user, in memory only: gone on restart or disconnect, never written to
/// the database or a file (the database travels to other environments and backups, the secret must not).
/// </summary>
public sealed class RuntimeRemoteStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, RemoteDefinition>> remotes = new();

    public IReadOnlyList<RemoteDefinition> GetAll(Guid userKey)
        => this.remotes.TryGetValue(userKey, out var forUser) ? forUser.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList() : [];

    public void Set(Guid userKey, RemoteDefinition remote)
        => this.remotes.GetOrAdd(userKey, _ => new ConcurrentDictionary<string, RemoteDefinition>(StringComparer.Ordinal))[remote.Alias] = remote;

    public bool Remove(Guid userKey, string alias)
        => this.remotes.TryGetValue(userKey, out var forUser) && forUser.TryRemove(alias, out _);
}