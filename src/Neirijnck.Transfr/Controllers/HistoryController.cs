using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neirijnck.Transfr.History;
using Neirijnck.Transfr.Models;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Security;

namespace Neirijnck.Transfr.Controllers;

public class HistoryController : TransfrApiControllerBase
{
    /// <summary>Transfers per page when the view doesn't say.</summary>
    public const int DefaultTake = 50;

    /// <summary>The most transfers per page.</summary>
    public const int MaxTake = 100;

    private readonly RemoteRegistry registry;
    private readonly TransferHistoryService history;
    private readonly IBackOfficeSecurityAccessor backOfficeSecurityAccessor;

    public HistoryController(RemoteRegistry registry, TransferHistoryService history, IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        this.registry = registry;
        this.history = history;
        this.backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    /// <summary>
    /// The transfers from here to the remote, newest first, as the copy history recorded them (the documents each one
    /// wrote). Only reads here: the remote isn't contacted. 400 when take or before is out of range.
    /// </summary>
    [HttpGet("remotes/{alias}/history")]
    [ProducesResponseType<TransferHistoryResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(string alias, int? before, int take = DefaultTake)
    {
        if (take is < 1 or > MaxTake || before is < 1)
        {
            return this.Problem(
                title: "The page is out of range",
                detail: $"take must be 1 to {MaxTake}, before at least 1.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var remote = this.registry.FindValid(CurrentUserKey(this.backOfficeSecurityAccessor), alias);
        if (remote is null)
        {
            return this.NotFound();
        }

        var page = await this.history.ReadAsync(remote, before, take);
        return this.Ok(new TransferHistoryResponseModel(
            page.Transfers
                .Select(transfer => new TransferHistoryItemResponseModel(
                    transfer.At,
                    page.UserNames.GetValueOrDefault(transfer.UserKey),
                    transfer.Documents
                        .Select(document => new TransferHistoryDocumentResponseModel(
                            document.DocumentKey,
                            page.DocumentNames.GetValueOrDefault(document.DocumentKey),
                            document.Outcome,
                            document.Kind.ToString()))
                        .ToList()))
                .ToList(),
            page.NextBefore));
    }
}