namespace Neirijnck.Transfr.Core.Schema;

/// <summary>
/// Why a schema item stands in the way of a copy.
/// </summary>
public enum SchemaProblemReason
{
    MissingOnRemote,
    MissingLocally,
    Differs,
}