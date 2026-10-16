using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Local;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// A dry run with what the view needs to explain it.
/// </summary>
/// <param name="Rows">The plan, media first.</param>
/// <param name="Names">Names behind the keys in the notes: documents and media here, namesakes on the target, document types.</param>
/// <param name="SelectableHere">Documents named in a note that exist here, so they can be added to the selection.</param>
/// <param name="DocumentTypes">The name and property labels of each row's document type here.</param>
/// <param name="SeparateInstall">Evidence that the remote was installed separately (empty when there is none).</param>
/// <param name="RemoteRequests">Requests it cost on the remote.</param>
/// <param name="SourceJson">The JSON here of every planned document, by key: what a transfer writes.</param>
/// <param name="TargetJson">The remote's JSON of every planned document it has, by key: a field-level write builds on it.</param>
/// <param name="RemoteChanges">Who last changed each conflicting document on the remote, where that could be read.</param>
/// <param name="LastCopies">The latest copy of each planned document to this remote, for those copied before.</param>
public sealed record CopyPlanResult(
    IReadOnlyList<PlanRow> Rows,
    IReadOnlyDictionary<Guid, string> Names,
    IReadOnlySet<Guid> SelectableHere,
    IReadOnlyDictionary<Guid, ContentTypeNames> DocumentTypes,
    IReadOnlyList<SeparateInstallEvidence> SeparateInstall,
    int RemoteRequests,
    IReadOnlyDictionary<Guid, string> SourceJson,
    IReadOnlyDictionary<Guid, string> TargetJson,
    IReadOnlyDictionary<Guid, RemoteChangeInfo> RemoteChanges,
    IReadOnlyDictionary<Guid, LastCopyInfo> LastCopies);