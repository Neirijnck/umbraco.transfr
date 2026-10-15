using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// One document copied by Transfr: the baseline for conflict detection (the target's last save right after our copy)
/// and, later, the history view. Rows are only ever added.
/// </summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class CopyHistoryDto
{
    public const string TableName = "transfrCopyHistory";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>The environment copied to or from (<see cref="Core.Remote.RemoteIdentity"/>).</summary>
    [Column("remote")]
    [Length(500)]
    [Index(IndexTypes.NonClustered, Name = "IX_transfrCopyHistory_remote_documentKey", ForColumns = "remote,documentKey")]
    public string Remote { get; set; } = string.Empty;

    [Column("documentKey")]
    public Guid DocumentKey { get; set; }

    /// <summary>ToRemote (from here to the remote) or, later, FromRemote.</summary>
    [Column("direction")]
    [Length(20)]
    public string Direction { get; set; } = string.Empty;

    /// <summary>Create or Update.</summary>
    [Column("outcome")]
    [Length(20)]
    public string Outcome { get; set; } = string.Empty;

    /// <summary>The backoffice user who ran the copy.</summary>
    [Column("userKey")]
    public Guid UserKey { get; set; }

    /// <summary>When the copy was written (UTC).</summary>
    [Column("copiedAt")]
    public DateTime CopiedAt { get; set; }

    /// <summary>
    /// The target's last save right after the copy, round-trip text (the API's own precision and offset, so it compares
    /// exactly with what the target reports later).
    /// </summary>
    [Column("targetUpdateDate")]
    [Length(40)]
    public string TargetUpdateDate { get; set; } = string.Empty;
}