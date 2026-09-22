namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices, in isolation, a structure which would let the comparison query find its next extreme without rescanning: the distinct values in a set ordered by the caller's comparer, each carrying how many times it occurs
/// </summary>
/// <remarks>
/// <see cref="ComparisonEvictionBenchmarks"/> measured the rescan an eviction causes today. Before that structure is built into the query, both sides of the substitution are priced here, at the sizes measured there: building it over every value, and the pair of changes an eviction arm makes — each change forgets one occurrence of the old value, records one of the new, and reads the maximum. The rescan arms do what the query does on eviction, one comparison per element over a list, so that the two costs are read side by side without the query around either. Values are compared under the invariant culture, as there
/// </remarks>
[MemoryDiagnoser]
public class SortedCountPricingBenchmarks
{
    /// <summary>
    /// A distinct value and the number of times it occurs
    /// </summary>
    sealed class Occurrences(string value)
    {
        public int Count;
        public string Value = value;
    }

    sealed class OccurrencesComparer(IComparer<string> comparer) :
        IComparer<Occurrences>
    {
        public int Compare(Occurrences? x, Occurrences? y) =>
            comparer.Compare(x!.Value, y!.Value);
    }

    sealed class SortedCounts
    {
        public SortedCounts(IReadOnlyList<string> values)
        {
            set = new SortedSet<Occurrences>(new OccurrencesComparer(StringComparer.InvariantCulture));
            for (int i = 0, ii = values.Count; i < ii; ++i)
                Add(values[i]);
        }

        readonly Occurrences probe = new(string.Empty);
        readonly SortedSet<Occurrences> set;

        public string Max =>
            set.Max!.Value;

        public void Add(string value)
        {
            probe.Value = value;
            if (set.TryGetValue(probe, out var occurrences))
                ++occurrences.Count;
            else
                set.Add(new Occurrences(value) { Count = 1 });
        }

        public void Remove(string value)
        {
            probe.Value = value;
            if (set.TryGetValue(probe, out var occurrences) && --occurrences.Count == 0)
                set.Remove(occurrences);
        }
    }

    string evicted256 = null!;
    string evicted4096 = null!;
    SortedCounts sorted256 = null!;
    SortedCounts sorted4096 = null!;
    List<string> values256 = null!;
    List<string> values4096 = null!;

    static List<string> CreateValues(int count, int seed)
    {
        var random = new Random(seed);
        var values = new List<string>(count);
        for (var i = 0; i < count; ++i)
            values.Add($"{random.Next():x8}{random.Next():x8}");
        return values;
    }

    static string Rescan(List<string> values)
    {
        var comparer = StringComparer.InvariantCulture;
        var max = values[0];
        for (int i = 1, ii = values.Count; i < ii; ++i)
            if (comparer.Compare(values[i], max) > 0)
                max = values[i];
        return max;
    }

    [Benchmark]
    public int RescanPair256() =>
        Rescan(values256).Length + Rescan(values256).Length;

    [Benchmark(Baseline = true)]
    public int RescanPair4096() =>
        Rescan(values4096).Length + Rescan(values4096).Length;

    [GlobalSetup]
    public void Setup()
    {
        values256 = CreateValues(256, 1);
        values4096 = CreateValues(4096, 2);
        evicted256 = values256.Min(StringComparer.InvariantCulture)!;
        evicted4096 = values4096.Min(StringComparer.InvariantCulture)!;
        sorted256 = new(values256);
        sorted4096 = new(values4096);
    }

    [Benchmark]
    public string SortedBuild256() =>
        new SortedCounts(values256).Max;

    [Benchmark]
    public string SortedBuild4096() =>
        new SortedCounts(values4096).Max;

    static int SortedPair(SortedCounts sorted, string evicted)
    {
        sorted.Remove(evicted);
        sorted.Add("zzzzzzzz");
        var first = sorted.Max.Length;
        sorted.Remove("zzzzzzzz");
        sorted.Add(evicted);
        return first + sorted.Max.Length;
    }

    [Benchmark]
    public int SortedPair256() =>
        SortedPair(sorted256, evicted256);

    [Benchmark]
    public int SortedPair4096() =>
        SortedPair(sorted4096, evicted4096);
}
