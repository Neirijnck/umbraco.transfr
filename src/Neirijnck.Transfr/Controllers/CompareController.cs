using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Core.Transfer;
using Neirijnck.Transfr.Core.Schema;
using Neirijnck.Transfr.Core.Tree;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Planning;
using Neirijnck.Transfr.Remote;
using Neirijnck.Transfr.Transfer;
using Umbraco.Cms.Core.Security;

namespace Neirijnck.Transfr.Controllers;

public partial class CompareController : TransfrApiControllerBase
{
    private readonly RemoteRegistry registry;
    private readonly TreeCompareService treeCompare;
    private readonly DocumentDiffService documentDiff;
    private readonly CopyPlanService copyPlan;
    private readonly TransferService transfer;
    private readonly IBackOfficeSecurityAccessor backOfficeSecurityAccessor;
    private readonly ILogger<CompareController> logger;

    public CompareController(
        RemoteRegistry registry,
        TreeCompareService treeCompare,
        DocumentDiffService documentDiff,
        CopyPlanService copyPlan,
        TransferService transfer,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<CompareController> logger)
    {
        this.registry = registry;
        this.treeCompare = treeCompare;
        this.documentDiff = documentDiff;
        this.copyPlan = copyPlan;
        this.transfer = transfer;
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
    /// Compares the children of one media folder (no parentKey = the root of the media tree) with the same level on the
    /// remote.
    /// </summary>
    [HttpGet("remotes/{alias}/media/tree")]
    [ProducesResponseType<TreeLevelResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> CompareMediaTreeLevel(string alias, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, parentKey, async remote =>
        {
            var comparison = await this.treeCompare.CompareMediaLevelAsync(remote, parentKey, cancellationToken);
            return this.Ok(new TreeLevelResponseModel(RowsOf(comparison), SeparateInstallOf([comparison]), comparison.RemoteRequests));
        });

    /// <summary>
    /// Compares every level below one media folder (no parentKey = the whole media tree) with the remote, breadth first,
    /// up to a node limit (Truncated says it was reached).
    /// </summary>
    [HttpGet("remotes/{alias}/media/subtree")]
    [ProducesResponseType<SubtreeResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> CompareMediaSubtree(string alias, Guid? parentKey, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, parentKey, async remote =>
        {
            var subtree = await this.treeCompare.CompareMediaSubtreeAsync(remote, parentKey, cancellationToken);
            var levels = subtree.Levels.Select(level => new SubtreeLevelResponseModel(level.ParentKey, RowsOf(level.Level))).ToList();
            return this.Ok(new SubtreeResponseModel(levels, SeparateInstallOf(subtree.Levels.Select(level => level.Level)), subtree.RemoteRequests, subtree.Truncated));
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

    /// <summary>
    /// One media item on both sides, value by value, with its files on both sides for the diff view. 404 when it isn't
    /// on both sides.
    /// </summary>
    [HttpGet("remotes/{alias}/media/{key:guid}/diff")]
    [ProducesResponseType<DocumentDiffResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> DiffMedia(string alias, Guid key, CancellationToken cancellationToken)
        => this.CompareWithAsync(alias, key, async remote =>
        {
            var result = await this.documentDiff.DiffMediaAsync(remote, key, cancellationToken);
            return result is { } found
                ? this.Ok(DocumentDiffResponses.From(found.Diff, found.Names, found.Files))
                : this.Problem(title: "The media item is not on both sides", statusCode: StatusCodes.Status404NotFound);
        });

    /// <summary>
    /// The dry run of copying the selected documents (each with or without everything below it) from here to the
    /// remote. It only reads, on both sides. 400 when the selection is empty, unknown here or too large.
    /// </summary>
    [HttpPost("remotes/{alias}/plan")]
    [ProducesResponseType<CopyPlanResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PlanCopy(string alias, CopyPlanRequestModel request, CancellationToken cancellationToken)
    {
        if (request.Selection is not { Count: > 0 })
        {
            return this.Problem(title: "Select at least one node or media item", statusCode: StatusCodes.Status400BadRequest);
        }

        var (selection, invalid) = ReadSelection(request.Selection);
        if (invalid is not null)
        {
            return this.Problem(title: "The selection can't be read", detail: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        return await this.CompareWithAsync(alias, null, async remote =>
        {
            try
            {
                return this.Ok(PlanResponses.From(await this.copyPlan.PlanAsync(remote, selection, cancellationToken)));
            }
            catch (PlanSelectionException exception)
            {
                var keys = exception.Keys.Count == 0 ? string.Empty : " " + string.Join(", ", exception.Keys);
                return this.Problem(title: "The selection can't be planned", detail: exception.Message + keys, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    /// <summary>
    /// Carries out a dry run on the remote: plans again and writes only when every document row is as DryRun shows it
    /// (status Refused with the differences otherwise, Busy when another transfer to the remote is running). The report
    /// says per row what happened; a failed row stops the rest, and what was written stays written. Always 200 with the
    /// report: the backoffice HTTP client replaces the body of a 409 with a generic error (measured), so the reasons
    /// would never reach the view.
    /// </summary>
    [HttpPost("remotes/{alias}/transfer")]
    [ProducesResponseType<TransferResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Transfer(string alias, TransferRequestModel request, CancellationToken cancellationToken)
    {
        if (request.Selection is not { Count: > 0 })
        {
            return this.Problem(title: "Select at least one node or media item", statusCode: StatusCodes.Status400BadRequest);
        }

        var dryRun = new List<DryRunRow>();
        foreach (var row in request.DryRun ?? [])
        {
            if (!Enum.TryParse<PlanOutcome>(row.Outcome, ignoreCase: false, out var outcome) || !Enum.IsDefined(outcome))
            {
                return this.Problem(title: "The dry run names an unknown outcome", detail: row.Outcome, statusCode: StatusCodes.Status400BadRequest);
            }

            dryRun.Add(new DryRunRow(row.Key, outcome, row.Fingerprint));
        }

        var (selection, invalid) = ReadSelection(request.Selection);
        if (invalid is not null)
        {
            return this.Problem(title: "The selection can't be read", detail: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        var overwrite = (request.Overwrite ?? []).ToHashSet();
        var userKey = CurrentUserKey(this.backOfficeSecurityAccessor);
        return await this.CompareWithAsync(alias, null, async remote =>
        {
            try
            {
                var result = await this.transfer.TransferAsync(remote, userKey, selection, dryRun, overwrite, cancellationToken);
                return this.Ok(TransferResponses.From(result));
            }
            catch (PlanSelectionException exception)
            {
                var keys = exception.Keys.Count == 0 ? string.Empty : " " + string.Join(", ", exception.Keys);
                return this.Problem(title: "The transfer can't be planned", detail: exception.Message + keys, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    // Finds the user's remote (404 when unknown) and turns a refused sign-in or a failed remote read into a 502.
    // The selection as the planner takes it, or why it can't: a field-level pick is one node (no descendants) with at
    // least one readable field.
    private static (List<PlanSelection> Selection, string? Error) ReadSelection(IReadOnlyList<PlanSelectionRequestModel> request)
    {
        var selection = new List<PlanSelection>();
        foreach (var choice in request)
        {
            if (choice.Kind is not (null or nameof(PlanItemKind.Document) or nameof(PlanItemKind.Media)))
            {
                return ([], $"{choice.Key}: the kind is Document or Media, not {choice.Kind}.");
            }

            if (choice.Kind == nameof(PlanItemKind.Media))
            {
                if (choice.Fields is not null)
                {
                    return ([], $"{choice.Key}: a media item is picked whole, without fields.");
                }

                selection.Add(new PlanSelection(choice.Key, choice.IncludeDescendants, Kind: PlanItemKind.Media));
                continue;
            }

            if (choice.Fields is null)
            {
                selection.Add(new PlanSelection(choice.Key, choice.IncludeDescendants));
                continue;
            }

            if (choice.IncludeDescendants || choice.Fields.Count == 0)
            {
                return ([], $"{choice.Key}: fields are picked for one node, without what is below it, and at least one.");
            }

            var fields = new List<FieldChoice>();
            foreach (var field in choice.Fields)
            {
                var (read, error) = FieldChoice.Read(field.Kind, field.Alias, field.Culture, field.Segment);
                if (read is null)
                {
                    return ([], $"{choice.Key}: {error}");
                }

                fields.Add(read);
            }

            selection.Add(new PlanSelection(choice.Key, false, fields));
        }

        return (selection, null);
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparing below {ParentKey} with (or planning a copy to) remote {Alias} failed")]
    private static partial void LogCompareFailed(ILogger logger, Guid? parentKey, string alias, Exception exception);

    private static List<SeparateInstallResponseModel> SeparateInstallOf(IEnumerable<TreeLevelComparison> levels)
        => levels.SelectMany(level => level.Schema.SeparateInstall)
            .DistinctBy(evidence => evidence.LocalKey)
            .Select(ResponseMapping.From)
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
            schema is null || schema.IsOk ? null : schema.Problems.Select(ResponseMapping.From).ToList());

    private static TreeRowContentResponseModel ToResponse(NodeContentComparison content)
    {
        if (content.Comparison is null)
        {
            return new TreeRowContentResponseModel("Failed", [], content.Error);
        }

        var differences = content.Comparison.Differences.Select(ResponseMapping.From).ToList();
        return new TreeRowContentResponseModel(content.Comparison.IsIdentical ? "Identical" : "Different", differences, null);
    }
}