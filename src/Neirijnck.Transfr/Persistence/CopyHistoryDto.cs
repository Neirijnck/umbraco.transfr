using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// One document or media item copied by Transfr: the baseline for conflict detection (both sides' last save around our
/// copy, <see cref="Core.History.CopyBaseline"/>) and the history view. Rows are only ever added.
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

    /// <summary>The document or media item (keys are unique across both), the same key on both sides.</summary>
    [Column("documentKey")]
    public Guid DocumentKey { get; set; }

    /// <summary>
    /// Document or Media (<see cref="Core.Planning.PlanItemKind"/>); rows from before media were copied are documents.
    /// </summary>
    [Column("kind")]
    [Length(20)]
    [Constraint(Default = "Document")]
    public string Kind { get; set; } = "Document";

    /// <summary>ToRemote or FromRemote (<see cref="Core.History.CopyDirection"/>).</summary>
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

    /// <summary>
    /// The source's last save when it was copied, round-trip text like <see cref="TargetUpdateDate"/>: the baseline for a
    /// later copy the other way. Null on rows recorded before both sides were kept.
    /// </summary>
    [Column("sourceUpdateDate")]
    [Length(40)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? SourceUpdateDate { get; set; }
}