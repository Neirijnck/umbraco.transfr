using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Security;

namespace Neirijnck.Transfr.Controllers;

public partial class CompareController : TransfrApiControllerBase
{
    private readonly RemoteRegistry registry;
    private readonly TreeCompareService treeCompare;
    private readonly IBackOfficeSecurityAccessor backOfficeSecurityAccessor;
    private readonly ILogger<CompareController> logger;

    public CompareController(
        RemoteRegistry registry,
        TreeCompareService treeCompare,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<CompareController> logger)
    {
        this.registry = registry;
        this.treeCompare = treeCompare;
        this.backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        this.logger = logger;
    }

    /// <summary>
    /// Compares the children of one document (no parentKey = the root) with the same level on the remote.
    /// </summary>
    [HttpGet("remotes/{alias}/tree")]
    [ProducesResponseType<TreeLevelResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CompareTreeLevel(string alias, Guid? parentKey, CancellationToken cancellationToken)
    {
        var remote = this.registry.FindValid(CurrentUserKey(this.backOfficeSecurityAccessor), alias);
        if (remote is null)
        {
            return this.NotFound();
        }

        try
        {
            var comparison = await this.treeCompare.CompareLevelAsync(remote, parentKey, cancellationToken);
            return this.Ok(new TreeLevelResponseModel(comparison.Rows.Select(ToResponse).ToList(), comparison.RemoteRequests));
        }
        catch (RemoteAuthenticationException exception)
        {
            LogCompareFailed(this.logger, parentKey, alias, exception);
            return this.Problem(title: "Signing in to the remote failed", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException exception)
        {
            LogCompareFailed(this.logger, parentKey, alias, exception);
            return this.Problem(title: "Reading the remote failed", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparing tree level {ParentKey} with remote {Alias} failed")]
    private static partial void LogCompareFailed(ILogger logger, Guid? parentKey, string alias, Exception exception);

    private static TreeRowResponseModel ToResponse(TreeCompareRow row)
        => new(
            row.Key,
            row.Status.ToString(),
            row.Local is null ? null : new TreeSideResponseModel(row.Local.Name, row.Local.HasChildren),
            row.Remote is null ? null : new TreeSideResponseModel(row.Remote.Name, row.Remote.HasChildren),
            row.OtherParentKey,
            row.SameNameKey);
}