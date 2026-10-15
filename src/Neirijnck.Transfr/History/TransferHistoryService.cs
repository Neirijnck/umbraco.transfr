using Neirijnck.Transfr.Core.Remote;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Persistence;
using Umbraco.Cms.Core.Services;

namespace Neirijnck.Transfr.History;

/// <summary>
/// Reads the transfers from here to a remote a page at a time, with the names of the users and documents here.
/// </summary>
public sealed class TransferHistoryService
{
    private readonly CopyHistoryStore history;
    private readonly LocalTreeReader localTree;
    private readonly IUserService userService;
    private readonly ILanguageService languageService;

    public TransferHistoryService(CopyHistoryStore history, LocalTreeReader localTree, IUserService userService, ILanguageService languageService)
    {
        this.history = history;
        this.localTree = localTree;
        this.userService = userService;
        this.languageService = languageService;
    }

    /// <summary>Up to <paramref name="take"/> transfers recorded before the row <paramref name="before"/> (null = the latest).</summary>
    public async Task<TransferHistoryPage> ReadAsync(RemoteDefinition remote, int? before, int take)
    {
        var (transfers, nextBefore) = this.history.ReadTransfers(remote, before, take);

        var userKeys = transfers.Select(transfer => transfer.UserKey).Distinct().ToList();
        var users = userKeys.Count == 0 ? [] : await this.userService.GetAsync(userKeys);

        // Documents deleted or in the recycle bin here since have no name.
        var documentKeys = transfers.SelectMany(transfer => transfer.Documents).Select(document => document.DocumentKey).Distinct().ToList();
        var culture = await this.languageService.GetDefaultIsoCodeAsync();
        var documents = this.localTree.Describe(documentKeys, culture);

        return new TransferHistoryPage(
            transfers,
            nextBefore,
            users.ToDictionary(user => user.Key, user => user.Name ?? string.Empty),
            documents.ToDictionary(pair => pair.Key, pair => pair.Value.Name));
    }
}