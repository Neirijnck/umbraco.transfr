using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Migrations;

/// <summary>
/// Gives the Administrators group access to the Transfr section, so nobody has to tick it by hand. Harmless where no
/// remote is configured: the section is then never registered, so it shows nowhere.
/// Leaves the group alone when it is missing or already has the section.
/// </summary>
public sealed partial class GrantSectionToAdministrators : AsyncMigrationBase
{
    private readonly IUserGroupService userGroupService;
    private readonly ILogger<GrantSectionToAdministrators> logger;

    public GrantSectionToAdministrators(IMigrationContext context, IUserGroupService userGroupService, ILogger<GrantSectionToAdministrators> logger)
        : base(context)
    {
        this.userGroupService = userGroupService;
        this.logger = logger;
    }

    protected override async Task MigrateAsync()
    {
        var administrators = await this.userGroupService.GetAsync(UmbracoConstants.Security.AdminGroupAlias);
        if (administrators is null)
        {
            LogNoAdministrators(this.logger, UmbracoConstants.Security.AdminGroupAlias);
            return;
        }

        if (administrators.AllowedSections.Contains(Constants.SectionAlias, StringComparer.Ordinal))
        {
            return;
        }

        administrators.AddAllowedSection(Constants.SectionAlias);
        var result = await this.userGroupService.UpdateAsync(administrators, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Granting the Transfr section to '{UmbracoConstants.Security.AdminGroupAlias}' failed: {result.Status}");
        }

        LogGranted(this.logger, UmbracoConstants.Security.AdminGroupAlias);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Granted the Transfr section to user group {GroupAlias}")]
    private static partial void LogGranted(ILogger logger, string groupAlias);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User group {GroupAlias} not found; the Transfr section was not granted")]
    private static partial void LogNoAdministrators(ILogger logger, string groupAlias);
}