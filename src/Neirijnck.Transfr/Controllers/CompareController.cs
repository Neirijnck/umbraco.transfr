using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Schema;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Security;

namespace Neirijnck.Transfr.Controllers;

public partial class CompareController : TransfrApiControllerBase
{
    private readonly RemoteRegistry registry;
    private readonly TreeCompareService treeCompare;
    private readonly DocumentDiffService documentDiff;
    private readonly IBackOfficeSecurityAccessor backOfficeSecurityAccessor;
    private readonly ILogger<CompareController> logger;

    public CompareController(
        RemoteRegistry registry,
        TreeCompareService treeCompare,
        DocumentDiffService documentDiff,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<CompareController> logger)
    {
        this.registry = registry;
        this.treeCompare = treeCompare;
        this.documentDiff = documentDiff;
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
    public Task<IActionResult> CompareTreeLevel(string alias, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, parentKey, async remote =>
        {
            var comparison = await this.treeCompare.CompareLevelAsync(remote, parentKey, cancellationToken);
            return this.Ok(new TreeLevelResponseModel(RowsOf(comparison), SeparateInstallOf([comparison]), comparison.RemoteRequests));
        });

    /// <summary>
    /// Compares every level below one document (no parentKey = the whole tree) with the remote, breadth first, up to a
    /// node limit (Truncated says it was reached).
    /// </summary>
    [HttpGet("remotes/{alias}/subtree")]
    [ProducesResponseType<SubtreeResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> CompareSubtree(string alias, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, parentKey, async remote =>
        {
            var subtree = await this.treeCompare.CompareSubtreeAsync(remote, parentKey, cancellationToken);
            var levels = subtree.Levels.Select(level => new SubtreeLevelResponseModel(level.ParentKey, RowsOf(level.Level))).ToList();
            var separateInstall = SeparateInstallOf(subtree.Levels.Select(level => level.Level));
            return this.Ok(new SubtreeResponseModel(levels, separateInstall, subtree.RemoteRequests, subtree.Truncated));
        });

    /// <summary>
    /// One document on both sides, field by field and block by block, for the diff view. 404 when it isn't on both sides.
    /// </summary>
    [HttpGet("remotes/{alias}/document/{key:guid}/diff")]
    [ProducesResponseType<DocumentDiffResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> DiffDocument(string alias, Guid key, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, key, async remote =>
        {
            var result = await this.documentDiff.DiffAsync(remote, key, cancellationToken);
            return result is { } found
                ? this.Ok(DocumentDiffResponses.From(found.Diff, found.Names))
                : this.Problem(title: "The document is not on both sides", statusCode: StatusCodes.Status404NotFound);
        });

    // Finds the user's remote (404 when unknown) and turns a refused sign-in or a failed remote read into a 502.
    private async Task<IActionResult> CompareWithAsync(string alias, Guid? parentKey, Func<RemoteDefinition, Task<IActionResult>> compare)
    {
        var remote = this.registry.FindValid(CurrentUserKey(this.backOfficeSecurityAccessor), alias);
        if (remote is null)
        {
            return this.NotFound();
        }

        try
        {
            return await compare(remote);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparing below {ParentKey} with remote {Alias} failed")]
    private static partial void LogCompareFailed(ILogger logger, Guid? parentKey, string alias, Exception exception);

    private static List<SeparateInstallResponseModel> SeparateInstallOf(IEnumerable<TreeLevelComparison> levels)
        => levels.SelectMany(level => level.Schema.SeparateInstall)
            .DistinctBy(evidence => evidence.LocalKey)
            .Select(evidence => new SeparateInstallResponseModel(evidence.Alias, evidence.Name, evidence.LocalKey, evidence.RemoteKey))
            .ToList();

    private static List<TreeRowResponseModel> RowsOf(TreeLevelComparison comparison)
        => comparison.Rows
            .Select(row =>
            {
                var check = TreeCompareService.DocumentTypeOf(row) is { } documentType ? comparison.Schema.Checks.GetValueOrDefault(documentType) : null;
                return ToResponse(row, comparison.Contents.GetValueOrDefault(row.Key), check, comparison.DocumentTypeNames);
            })
            .ToList();

    private static TreeSideResponseModel? SideOf(TreeNode? node, IReadOnlyDictionary<Guid, string> typeNames)
        => node is null
            ? null
            : new TreeSideResponseModel(
                node.Name,
                node.HasChildren,
                node.DocumentTypeIcon,
                node.DocumentTypeKey is { } type ? typeNames.GetValueOrDefault(type) : null);

    private static TreeRowResponseModel ToResponse(
        TreeCompareRow row,
        NodeContentComparison? content,
        SchemaCheck? schema,
        IReadOnlyDictionary<Guid, string> typeNames)
        => new(
            row.Key,
            row.Status.ToString(),
            SideOf(row.Local, typeNames),
            SideOf(row.Remote, typeNames),
            row.OtherParentKey,
            row.SameNameKey,
            content is null ? null : ToResponse(content),
            row.OrderDiffers,
            schema is null || schema.IsOk
                ? null
                : schema.Problems
                    .Select(problem => new SchemaProblemResponseModel(
                        problem.Kind.ToString(),
                        problem.Key,
                        problem.Name,
                        problem.IsElement,
                        problem.Reason.ToString(),
                        problem.Details))
                    .ToList());

    private static TreeRowContentResponseModel ToResponse(NodeContentComparison content)
    {
        if (content.Comparison is null)
        {
            return new TreeRowContentResponseModel("Failed", [], content.Error);
        }

        var differences = content.Comparison.Differences
            .Select(difference => new ContentDifferenceResponseModel(difference.Kind.ToString(), difference.Alias, difference.Culture, difference.Segment))
            .ToList();
        return new TreeRowContentResponseModel(content.Comparison.IsIdentical ? "Identical" : "Different", differences, null);
    }
}