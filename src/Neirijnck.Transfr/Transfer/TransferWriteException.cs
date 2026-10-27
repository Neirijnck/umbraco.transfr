namespace Neirijnck.Transfr.Transfer;

/// <summary>A write the target refused, where the transfer can't carry on (a sort).</summary>
internal sealed class TransferWriteException : Exception
{
    public TransferWriteException(string message)
        : base(message)
    {
    }
}