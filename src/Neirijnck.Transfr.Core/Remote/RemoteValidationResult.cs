namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// A configured remote and what is wrong with it; no errors means it may be used.
/// </summary>
public sealed record RemoteValidationResult(RemoteDefinition Remote, IReadOnlyList<string> Errors)
{
    public bool IsValid => this.Errors.Count == 0;
}