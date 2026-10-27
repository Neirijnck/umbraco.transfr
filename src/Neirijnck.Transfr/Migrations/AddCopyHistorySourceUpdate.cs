using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Persistence;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Neirijnck.Transfr.Migrations;

/// <summary>
/// Adds the source's last save to the copy history, so a copy the other way can tell whether its target changed since.
/// Rows recorded before have none (null). A table created after this column was added to <see cref="CopyHistoryDto"/>
/// has it already, and is left alone.
/// </summary>
public sealed partial class AddCopyHistorySourceUpdate : AsyncMigrationBase
{
    private const string Column = "sourceUpdateDate";

    private readonly ILogger<AddCopyHistorySourceUpdate> logger;

    public AddCopyHistorySourceUpdate(IMigrationContext context, ILogger<AddCopyHistorySourceUpdate> logger)
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