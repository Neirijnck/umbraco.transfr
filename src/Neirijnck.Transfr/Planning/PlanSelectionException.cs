namespace Neirijnck.Transfr.Planning;

/// <summary>
/// The selection can't be planned: documents that don't exist here, or too many of them.
/// </summary>
public sealed class PlanSelectionException : Exception
{
    public PlanSelectionException()
    {
        this.Keys = [];
    }

    public PlanSelectionException(string message)
        : base(message)
    {
        this.Keys = [];
    }

    public PlanSelectionException(string message, Exception innerException)
        : base(message, innerException)
    {
        this.Keys = [];
    }

    public PlanSelectionException(string message, IReadOnlyList<Guid> keys)
        : base(message)
    {
        this.Keys = keys;
    }

    /// <summary>The documents it is about, if any.</summary>
    public IReadOnlyList<Guid> Keys { get; }
}