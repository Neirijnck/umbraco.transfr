namespace Neirijnck.Transfr.Remote;

/// <summary>
/// The remote did not hand out a token for the configured client credentials.
/// </summary>
public sealed class RemoteAuthenticationException : Exception
{
    public RemoteAuthenticationException()
    {
    }

    public RemoteAuthenticationException(string message)
        : base(message)
    {
    }

    public RemoteAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}