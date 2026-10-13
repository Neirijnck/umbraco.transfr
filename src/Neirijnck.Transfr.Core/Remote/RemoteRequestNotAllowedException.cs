namespace Neirijnck.Transfr.Core.Remote;

/// <summary>
/// A request to a remote was stopped before it was sent, because it is not on the allowlist or goes elsewhere.
/// </summary>
public sealed class RemoteRequestNotAllowedException : InvalidOperationException
{
    public RemoteRequestNotAllowedException()
    {
    }

    public RemoteRequestNotAllowedException(string message)
        : base(message)
    {
    }

    public RemoteRequestNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}