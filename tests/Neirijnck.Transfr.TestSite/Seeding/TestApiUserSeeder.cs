using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
// Inside Neirijnck.Transfr.* the bare name Constants resolves to the package's own Constants class.
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.TestSite.Seeding;

/// <summary>
/// Makes a test site usable as a Transfr remote: a user group with only Read, Create, Update, Publish and Sort on
/// documents (no Delete, Unpublish or Move), an API user in that group, and client credentials from configuration.
/// Runs on every boot when <c>TransfrTestSite:ApiUser:ClientSecret</c> is set, and only adds what is missing.
/// This is the setup a real remote gets by hand (see the README); the test sites get it in code.
/// </summary>
public sealed partial class TestApiUserSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string GroupAlias = "transfrRemote";
    public const string UserEmail = "transfr-remote@transfr.test";

    // The document permissions the remote's API user gets. Deliberately absent: Delete, Unpublish, Move.
    private static readonly string[] DocumentPermissions =
    [
        ActionBrowse.ActionLetter,
        ActionNew.ActionLetter,
        ActionUpdate.ActionLetter,
        ActionPublish.ActionLetter,
        ActionSort.ActionLetter,
        ActionDocumentPropertyRead.ActionLetter,
        ActionDocumentPropertyWrite.ActionLetter,
    ];

    private readonly IConfiguration configuration;
    private readonly IRuntimeState runtimeState;
    private readonly IUserGroupService userGroupService;
    private readonly IUserService userService;
    private readonly IBackOfficeUserClientCredentialsManager clientCredentialsManager;
    private readonly IShortStringHelper shortStringHelper;
    private readonly ILogger<TestApiUserSeeder> logger;

    public TestApiUserSeeder(
        IConfiguration configuration,
        IRuntimeState runtimeState,
        IUserGroupService userGroupService,
        IUserService userService,
        IBackOfficeUserClientCredentialsManager clientCredentialsManager,
        IShortStringHelper shortStringHelper,
        ILogger<TestApiUserSeeder> logger)
    {
        this.configuration = configuration;
        this.runtimeState = runtimeState;
        this.userGroupService = userGroupService;
        this.userService = userService;
        this.clientCredentialsManager = clientCredentialsManager;
        this.shortStringHelper = shortStringHelper;
        this.logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        var clientId = this.configuration["TransfrTestSite:ApiUser:ClientId"];
        var clientSecret = this.configuration["TransfrTestSite:ApiUser:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || this.runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        var group = await this.userGroupService.GetAsync(GroupAlias) ?? await this.CreateGroupAsync();
        var user = this.userService.GetByEmail(UserEmail) ?? await this.CreateUserAsync(group);

        // Client ids are stored with the "umbraco-back-office-" prefix, which the configured id already carries.
        var clientIds = await this.userService.GetClientIdsAsync(user.Key);
        if (clientIds.Contains(clientId, StringComparer.Ordinal))
        {
            return;
        }

        var saved = await this.clientCredentialsManager.SaveAsync(user.Key, clientId, clientSecret);
        if (!saved.Success)
        {
            throw new InvalidOperationException($"Adding client credentials '{clientId}' failed: {saved.Result}");
        }

        LogCreated(this.logger, clientId, user.Key);
    }

    private async Task<IUserGroup> CreateGroupAsync()
    {
        var group = new UserGroup(this.shortStringHelper)
        {
            Alias = GroupAlias,
            Name = "Transfr Remote",
            Icon = "icon-sync",
            HasAccessToAllLanguages = true,
            StartContentId = -1,
            StartMediaId = -1,
            Permissions = new HashSet<string>(DocumentPermissions),
        };
        group.AddAllowedSection(UmbracoConstants.Applications.Content);
        group.AddAllowedSection(UmbracoConstants.Applications.Media);

        var result = await this.userGroupService.CreateAsync(group, UmbracoConstants.Security.SuperUserKey);
        return result.Success
            ? result.Result
            : throw new InvalidOperationException($"Creating user group '{GroupAlias}' failed: {result.Status}");
    }

    private async Task<IUser> CreateUserAsync(IUserGroup group)
    {
        var result = await this.userService.CreateAsync(
            UmbracoConstants.Security.SuperUserKey,
            new UserCreateModel
            {
                Email = UserEmail,
                UserName = UserEmail,
                Name = "Transfr Remote",
                Kind = UserKind.Api,
                UserGroupKeys = new HashSet<Guid> { group.Key },
            },
            approveUser: true);
        return result.Success && result.Result.CreatedUser is not null
            ? result.Result.CreatedUser
            : throw new InvalidOperationException($"Creating API user '{UserEmail}' failed: {result.Status}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created client credentials {ClientId} for API user {UserKey}")]
    private static partial void LogCreated(ILogger logger, string clientId, Guid userKey);
}