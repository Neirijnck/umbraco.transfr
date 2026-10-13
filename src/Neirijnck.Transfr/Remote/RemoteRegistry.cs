using Microsoft.Extensions.Options;
using Neirijnck.Transfr.Configuration;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// The configured remotes, validated on every read so configuration changes apply without a restart.
/// </summary>
public sealed class RemoteRegistry
{
    private readonly IOptionsMonitor<TransfrOptions> options;

    public RemoteRegistry(IOptionsMonitor<TransfrOptions> options)
    {
        this.options = options;
    }

    public bool AnyConfigured => this.options.CurrentValue.Remotes.Count > 0;

    public IReadOnlyList<RemoteValidationResult> GetAll()
        => RemoteDefinitionValidator.Validate(
            this.options.CurrentValue.Remotes
                .Select(remote => new RemoteDefinition(remote.Alias, remote.Name, remote.BaseUrl, remote.ClientId, remote.ClientSecret))
                .ToList());

    /// <summary>
    /// The valid remote with this alias, or null: an invalid remote is never contacted.
    /// </summary>
    public RemoteDefinition? FindValid(string alias)
        => this.GetAll().FirstOrDefault(result => result.IsValid && string.Equals(result.Remote.Alias, alias, StringComparison.Ordinal))?.Remote;
}