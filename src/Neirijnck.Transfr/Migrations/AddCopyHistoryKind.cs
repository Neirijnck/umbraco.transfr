using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Persistence;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Neirijnck.Transfr.Migrations;

/// <summary>
/// Adds the kind (document or media) to the copy history. Rows recorded before media were copied are documents, which
/// the column's default says. A table created after this column was added to <see cref="CopyHistoryDto"/> has it
/// already, and is left alone.
/// </summary>
public sealed partial class AddCopyHistoryKind : AsyncMigrationBase
{
    private const string Column = "kind";

    private readonly ILogger<AddCopyHistoryKind> logger;

    public AddCopyHistoryKind(IMigrationContext context, ILogger<AddCopyHistoryKind> logger)
        : base(context)
    {
        this.logger = logger;
    }

    protected override Task MigrateAsync()
    {
        if (!this.ColumnExists(CopyHistoryDto.TableName, Column))
        {
            this.AddColumn<CopyHistoryDto>(CopyHistoryDto.TableName, Column);
            LogAdded(this.logger, CopyHistoryDto.TableName, Column);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Added the column {Column} to the Transfr copy history table {TableName}")]
    private static partial void LogAdded(ILogger logger, string tableName, string column);
}