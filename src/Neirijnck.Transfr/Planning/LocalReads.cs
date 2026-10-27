namespace Neirijnck.Transfr.Planning;

/// <summary>Reading many items here for a plan, whichever side this environment is.</summary>
internal static class LocalReads
{
    /// <summary>One item at a time (Umbraco's ambient scope is not thread-safe); unreadable ones are left out.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> EachAsync(IEnumerable<Guid> keys, Func<Guid, Task<string?>> read)
    {
        var found = new Dictionary<Guid, string>();
        foreach (var key in keys)
        {
            if (await read(key) is { } json)
            {
                found[key] = json;
            }
        }

        return found;
    }
}