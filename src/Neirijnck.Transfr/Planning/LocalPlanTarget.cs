using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Local;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.Planning;

/// <summary>
/// This environment as the side a copy writes to.
/// </summary>
internal sealed partial class LocalPlanTarget : IPlanTarget
{
    // Audit entries read per item to find its latest content change, as for a remote.
    private const int AuditEntries = 20;

    private readonly LocalTreeReader tree;
    private readonly LocalDocumentReader documents;
    private readonly LocalMediaReader media;
    private readonly IAuditService auditService;
    private readonly IUserIdKeyResolver userIdKeyResolver;
    private readonly IUserService userService;
    private readonly ILogger logger;

    public LocalPlanTarget(
        LocalTreeReader tree,
        LocalDocumentReader documents,
        LocalMediaReader media,
        IAuditService auditService,
        IUserIdKeyResolver userIdKeyResolver,
        IUserService userService,
        ILogger logger)
    {
        this.tree = tree;
        this.documents = documents;
        this.media = media;
        this.auditService = auditService;
        this.userIdKeyResolver = userIdKeyResolver;
        this.userService = userService;
        this.logger = logger;
    }

    public int Requests => 0;

    public Task<IReadOnlyDictionary<Guid, string>> ReadDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => LocalReads.EachAsync(keys, this.documents.ReadAsync);

    public Task<IReadOnlyDictionary<Guid, ItemLocation>> LocateDocumentsAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.LocateDocuments(keys));

    public Task<IReadOnlyList<TreeNode>> ReadLevelAsync(Guid? parentKey, string? culture, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.ReadLevel(parentKey, culture));

    public Task<IReadOnlyDictionary<Guid, (Guid? ParentKey, bool IsTrashed)>> LocateMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => Task.FromResult(this.tree.LocateMedia(keys));

    public Task<IReadOnlyDictionary<Guid, string>> ReadMediaAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken)
        => LocalReads.EachAsync(keys, this.media.ReadAsync);

    // The same audit log Umbraco's own audit-log endpoint serves, read the same way (its CreateDate as the timestamp, its
    // user id resolved to the user's key), with the same rule for what counts as a content change.
    public async Task<IReadOnlyDictionary<Guid, RemoteChangeInfo>> LastChangesAsync(IReadOnlyList<(PlanItemKind Kind, Guid Key)> keys, CancellationToken cancellationToken)
    {
        var changes = new Dictionary<Guid, (Guid? UserKey, DateTimeOffset At, string What)>();
        foreach (var (kind, key) in keys)
        {
            try
            {
                var page = await this.auditService.GetItemsByKeyAsync(
                    key,
                    kind == PlanItemKind.Media ? UmbracoObjectTypes.Media : UmbracoObjectTypes.Document,
                    0,
                    AuditEntries);
                if (page.Items.FirstOrDefault(item => ManagementApiAuditParser.IsContentChange(item.AuditType.ToString())) is { } latest)
                {
                    Guid? userKey = latest.UserId == 0 ? null : await this.userIdKeyResolver.GetAsync(latest.UserId);
                    changes[key] = (userKey, latest.CreateDate, latest.AuditType.ToString());
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogChangeUnread(this.logger, exception, kind, key);
            }
        }

        var userKeys = changes.Values.Select(change => change.UserKey).OfType<Guid>().Distinct().ToList();
        var names = userKeys.Count == 0
            ? new Dictionary<Guid, string>()
            : (await this.userService.GetAsync(userKeys)).ToDictionary(user => user.Key, user => user.Name ?? string.Empty);
        return changes.ToDictionary(
            pair => pair.Key,
            pair => new RemoteChangeInfo(pair.Value.UserKey is { } user ? names.GetValueOrDefault(user) : null, pair.Value.At, pair.Value.What));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read who last changed {Kind} {Key} here")]
    private static partial void LogChangeUnread(ILogger logger, Exception exception, PlanItemKind kind, Guid key);
}