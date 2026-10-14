namespace Neirijnck.Transfr.Core;

/// <summary>
/// Finds what kept its order: of items in one order with their positions in another, the ones outside the longest run
/// that is in the same order on both sides are the fewest that moved.
/// </summary>
internal static class OrderRun
{
    /// <summary>
    /// The indexes (into <paramref name="values"/>) of one longest strictly increasing subsequence (patience sorting,
    /// O(n log n)).
    /// </summary>
    public static HashSet<int> LongestIncreasing(List<int> values)
    {
        var tails = new List<int>();
        var previous = new int[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var low = 0;
            var high = tails.Count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (values[tails[middle]] < values[i])
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[i] = low > 0 ? tails[low - 1] : -1;
            if (low == tails.Count)
            {
                tails.Add(i);
            }
            else
            {
                tails[low] = i;
            }
        }

        var run = new HashSet<int>();
        for (var i = tails.Count > 0 ? tails[^1] : -1; i >= 0; i = previous[i])
        {
            run.Add(i);
        }

        return run;
    }
}