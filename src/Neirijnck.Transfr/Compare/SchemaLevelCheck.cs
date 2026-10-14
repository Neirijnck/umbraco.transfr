using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Compare;

/// <summary>
/// The schema checks for the document types of one tree level, evidence of a separately installed remote, and the
/// requests that cost on the remote (cached items cost none).
/// </summary>
public sealed record SchemaLevelCheck(
    IReadOnlyDictionary<Guid, SchemaCheck> Checks,
    IReadOnlyList<SeparateInstallEvidence> SeparateInstall,
    int RemoteRequests);