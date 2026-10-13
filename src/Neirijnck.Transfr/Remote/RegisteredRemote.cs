using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Remote;

/// <summary>
/// A remote available to the current user, with its validation result and origin.
/// </summary>
public sealed record RegisteredRemote(RemoteValidationResult Validation, RemoteSource Source)
{
    public RemoteDefinition Remote => this.Validation.Remote;
}