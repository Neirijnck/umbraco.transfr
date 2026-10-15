using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Persistence;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Neirijnck.Transfr.Migrations;

/// <summary>
/// Creates the copy history table. Leaves an existing table alone, so a database restored over a newer one keeps it.
/// </summary>
public sealed partial class CreateCopyHistory : AsyncMigrationBase
{
    private readonly ILogger<CreateCopyHistory> logger;

    public CreateCopyHistory(IMigrationContext context, ILogger<CreateCopyHistory> logger)
        : base(context)
    {
        this.logger = logger;
    }

    protected override Task MigrateAsync()
    {
        if (!this.TableExists(CopyHistoryDto.TableName))
        {
            this.Create.Table<CopyHistoryDto>().Do();
            LogCreated(this.logger, CopyHistoryDto.TableName);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the Transfr copy history table {TableName}")]
    private static partial void LogCreated(ILogger logger, string tableName);
}