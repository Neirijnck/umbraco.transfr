using System.Globalization;
using Neirijnck.Transfr.Core.Remote;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Neirijnck.Transfr.Persistence;

/// <summary>
/// Reads and adds to the copy history (<see cref="CopyHistoryDto"/>), per remote environment.
/// </summary>
public sealed class CopyHistoryStore
{
    // Keys per IN (...) query: well under SQL Server's 2100 parameters.
    private const int KeysPerQuery = 500;

    private const string ToRemote = "ToRemote";

    private readonly IScopeProvider scopeProvider;
    private readonly TimeProvider timeProvider;

    public CopyHistoryStore(IScopeProvider scopeProvider, TimeProvider timeProvider)
    {
        this.scopeProvider = scopeProvider;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// For each of these documents copied from here to the remote before: the latest copy. Documents never copied there
    /// are left out.
    /// </summary>
    public IReadOnlyDictionary<Guid, LastCopy> LatestCopies(RemoteDefinition remote, IReadOnlyCollection<Guid> keys)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(keys);
        var identity = RemoteIdentity.Of(remote.BaseUrl);

        var rows = new List<CopyHistoryDto>();
        using (var scope = this.scopeProvider.CreateScope(autoComplete: true))
        {
            foreach (var batch in keys.Distinct().Chunk(KeysPerQuery))
            {
                rows.AddRange(scope.Database.Fetch<CopyHistoryDto>(
                    "WHERE remote = @0 AND direction = @1 AND documentKey IN (@2)",
                    identity,
                    ToRemote,
                    batch));
            }
        }

        return rows
            .GroupBy(row => row.DocumentKey)
            .ToDictionary(group => group.Key, group => ToLastCopy(group.MaxBy(row => row.Id)!));
    }

    // copiedAt is written as UTC; the database hands it back without a kind.
    private static LastCopy ToLastCopy(CopyHistoryDto row)
        => new(
            DateTimeOffset.Parse(row.TargetUpdateDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            new DateTimeOffset(DateTime.SpecifyKind(row.CopiedAt, DateTimeKind.Utc)),
            row.UserKey);

    /// <summary>Records documents just copied from here to the remote, all in one transaction.</summary>
    public void RecordToRemote(RemoteDefinition remote, Guid userKey, IReadOnlyCollection<CopyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(records);
        var identity = RemoteIdentity.Of(remote.BaseUrl);
        var now = this.timeProvider.GetUtcNow().UtcDateTime;

        using var scope = this.scopeProvider.CreateScope();
        foreach (var record in records)
        {
            scope.Database.Insert(new CopyHistoryDto
            {
                Remote = identity,
                DocumentKey = record.DocumentKey,
                Direction = ToRemote,
                Outcome = record.Outcome,
                UserKey = userKey,
                CopiedAt = now,
                TargetUpdateDate = record.TargetUpdateDate.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        scope.Complete();
    }
}