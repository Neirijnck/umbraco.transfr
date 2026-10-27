using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Neirijnck.Transfr.Configuration;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// The remotes a user can work with: the configured ones plus those the user connected at runtime. Validated on every
/// read, so configuration changes apply without a restart.
/// </summary>
public sealed class RemoteRegistry
{
    private readonly IOptionsMonitor<TransfrOptions> options;
    private readonly IHostEnvironment hostEnvironment;
    private readonly RuntimeRemoteStore runtimeRemotes;

    public RemoteRegistry(IOptionsMonitor<TransfrOptions> options, IHostEnvironment hostEnvironment, RuntimeRemoteStore runtimeRemotes)
    {
        this.options = options;
        this.hostEnvironment = hostEnvironment;
        this.runtimeRemotes = runtimeRemotes;
    }

    public bool RuntimeRemotesAllowed
        => this.options.CurrentValue.AllowRuntimeRemotes ?? this.IsDevelopmentOrLocal;

    /// <summary>Whether copying from a remote to this environment is allowed here (<see cref="TransfrOptions.AllowPull"/>).</summary>
    public bool PullAllowed => this.options.CurrentValue.AllowPull ?? this.IsDevelopmentOrLocal;

    private bool IsDevelopmentOrLocal => this.hostEnvironment.IsDevelopment() || this.hostEnvironment.IsEnvironment("Local");

    /// <summary>
    /// Whether Transfr is available here at all: some remote is configured, or remotes may be connected at runtime.
    /// </summary>
    public bool IsEnabled => this.options.CurrentValue.Remotes.Count > 0 || this.RuntimeRemotesAllowed;

    public IReadOnlyList<RegisteredRemote> GetAll(Guid userKey)
    {
        var configured = this.options.CurrentValue.Remotes
            .Select(remote => new RemoteDefinition(remote.Alias, remote.Name, remote.BaseUrl, remote.ClientId, remote.ClientSecret))
            .ToList();
        var runtime = this.RuntimeRemotesAllowed ? this.runtimeRemotes.GetAll(userKey) : [];

        // Validated together, so a runtime alias that collides with a configured one invalidates both.
        var results = RemoteDefinitionValidator.Validate([.. configured, .. runtime]);
        return results
            .Select((result, index) => new RegisteredRemote(result, index < configured.Count ? RemoteSource.Configuration : RemoteSource.Runtime))
            .ToList();
    }

    /// <summary>
    /// The valid remote with this alias for the user, or null: an invalid remote is never contacted.
    /// </summary>
    public RemoteDefinition? FindValid(Guid userKey, string alias)
        => this.GetAll(userKey).FirstOrDefault(r => r.Validation.IsValid && string.Equals(r.Remote.Alias, alias, StringComparison.Ordinal))?.Remote;

    /// <summary>
    /// The validation errors a new runtime remote would have next to the user's other remotes. Reconnecting a runtime
    /// remote under the same alias replaces it, so that one doesn't count as a duplicate.
    /// </summary>
    public IReadOnlyList<string> ValidateNew(Guid userKey, RemoteDefinition remote)
    {
        var others = this.GetAll(userKey)
            .Where(r => r.Source == RemoteSource.Configuration || !string.Equals(r.Remote.Alias, remote.Alias, StringComparison.Ordinal))
            .Select(r => r.Remote);
        return RemoteDefinitionValidator.Validate([.. others, remote])[^1].Errors;
    }

    public void Connect(Guid userKey, RemoteDefinition remote) => this.runtimeRemotes.Set(userKey, remote);

    public bool Disconnect(Guid userKey, string alias) => this.runtimeRemotes.Remove(userKey, alias);
}