using System.Collections.Concurrent;
using System.Net;
using Neirijnck.Transfr.Core.Remote;

namespace Neirijnck.Transfr.Planning;

/// <summary>Reading many items from a remote for a plan, whichever side it is.</summary>
internal static class RemoteReads
{
    /// <summary>Remote requests in flight at once, as for a tree compare.</summary>
    public const int MaxParallel = 4;

    /// <summary>
    /// One request per key, a few at a time: absent when the remote doesn't have it (404). Any other failure fails the
    /// plan.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> EachAsync(
        RemoteDefinition remote,
        IReadOnlyCollection<Guid> keys,
        Func<RemoteDefinition, Guid, CancellationToken, Task<string>> read,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<Guid, string>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(keys, options, async (key, token) =>
        {
            try
            {
                found[key] = await read(remote, key, token);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Not on the remote.
            }
        });

        return found;
    }
}