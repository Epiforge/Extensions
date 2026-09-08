namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Reactive.Linq;

/// <summary>
/// Asks whether the cost of moving one element between groups grows with the size of the collection, in each library, by holding the number of changes fixed and varying the number of elements
/// </summary>
/// <remarks>
/// <c>ChainedComparisonBenchmarks</c> grouped a filtered view and found this library costing 243.9 ns per migration over a thousand admitted elements and 517.0 ns over five thousand, while DynamicData's stayed at 677.7 and 686.1. <b>A cost which grows with the collection where the alternative's does not is the same shape as the ordered query's, and that one turned out to have a crossover.</b> Grouping is currently the operator this library wins most clearly, so whether that win has a size limit has to be settled before it is claimed
/// </remarks>
/// <remarks>
/// The workload is a thousand changes at every size rather than one per element, for the reason <c>OrderedScaleComparisonBenchmarks</c> gives: changing every element would make the total grow whether the per-change cost did or not. The changed elements are spread by stride and each rank is exclusive-ored with 2, which changes the rank modulo sixteen, so <b>every change moves its element out of one group and into another</b> at every size
/// </remarks>
/// <remarks>
/// The arms which read after changing exist because a cost which comes back suspiciously cheap may belong to a view deferring its work until somebody looks, which the counts a probe checks would not reveal if the read is what performs the migration
/// </remarks>
[MemoryDiagnoser]
public class GroupedScaleComparisonBenchmarks
{
    const int changeCount = 1000;
    const int groupCount = 16;

    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IObservableCache<IGroup<BenchmarkPerson, string, int>, int> dynamicDataGroups = null!;
    IObservableCollectionQuery<IObservableGrouping<int, BenchmarkPerson>> groupBy = null!;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    int stride;

    [Params(1_000, 4_000, 10_000)]
    public int ElementCount { get; set; }

    void ChangeAThousandRanks()
    {
        for (var i = 0; i < changeCount; ++i)
            source[i * stride].Rank ^= 2;
    }

    /// <summary>
    /// What a thousand rank changes cost before anything observes them, which is the floor every arm below stands on and which should be flat across the sizes
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeAThousandRanksUnobserved() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksWithDynamicDataCache() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksWithExpressions() =>
        ChangeAThousandRanks();

    [Benchmark]
    public long ChangeAThousandRanksThenReadWithDynamicDataCache()
    {
        ChangeAThousandRanks();
        return CountAcrossGroups(DynamicDataGroupCount);
    }

    [Benchmark]
    public long ChangeAThousandRanksThenReadWithExpressions()
    {
        ChangeAThousandRanks();
        return CountAcrossGroups(ExpressionsGroupCount);
    }

    [GlobalCleanup(Targets = [nameof(ChangeAThousandRanksWithDynamicDataCache), nameof(ChangeAThousandRanksThenReadWithDynamicDataCache)])]
    public void CleanupDynamicData()
    {
        dynamicDataGroups.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Targets = [nameof(ChangeAThousandRanksWithExpressions), nameof(ChangeAThousandRanksThenReadWithExpressions)])]
    public void CleanupExpressions()
    {
        groupBy.Dispose();
        sourceQuery.Dispose();
    }

    static long CountAcrossGroups(Func<int, int> countInGroup)
    {
        var total = 0L;
        for (var key = 0; key < groupCount; ++key)
            total += countInGroup(key);
        return total;
    }

    int DynamicDataGroupCount(int key) =>
        dynamicDataGroups.Lookup(key) is { HasValue: true } found ? found.Value.Cache.Count : 0;

    int ExpressionsGroupCount(int key) =>
        groupBy.FirstOrDefault(grouping => grouping.Key == key) is { } found ? found.Count : 0;

    /// <summary>
    /// Moves one element between groups and back again, so that an arrangement which is not actually regrouping is caught here rather than reported as a fast one
    /// </summary>
    void Probe(Func<int, int> countInGroup, Func<int, IReadOnlyCollection<BenchmarkPerson>> group, string arm)
    {
        if (CountAcrossGroups(countInGroup) != ElementCount)
            throw new InvalidOperationException($"{arm} accounts for {CountAcrossGroups(countInGroup)} elements across its groups where the source has {ElementCount}");
        var subject = source[0];
        var held = subject.Rank;
        var wasIn = held % groupCount;
        var goesTo = (wasIn + 1) % groupCount;
        subject.Rank = held + 1;
        if (!group(goesTo).Contains(subject))
            throw new InvalidOperationException($"{arm} did not move an element into the group its key changed to");
        if (group(wasIn).Contains(subject))
            throw new InvalidOperationException($"{arm} left an element in the group its key changed away from");
        subject.Rank = held;
        if (CountAcrossGroups(countInGroup) != ElementCount)
            throw new InvalidOperationException($"{arm} lost or gained elements while a key moved and moved back");
    }

    [GlobalSetup(Targets = [nameof(ChangeAThousandRanksWithDynamicDataCache), nameof(ChangeAThousandRanksThenReadWithDynamicDataCache)])]
    public void SetupDynamicData()
    {
        SetupSource();
        dynamicDataCache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        dynamicDataCache.AddOrUpdate(source);
        dynamicDataGroups = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Group(person => person.Rank % groupCount)
            .AsObservableCache();
        Probe(DynamicDataGroupCount, key => dynamicDataGroups.Lookup(key) is { HasValue: true } found ? [.. found.Value.Cache.Items] : [], "DynamicData's grouping");
    }

    [GlobalSetup(Targets = [nameof(ChangeAThousandRanksWithExpressions), nameof(ChangeAThousandRanksThenReadWithExpressions)])]
    public void SetupExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        groupBy = sourceQuery.ObserveGroupBy(person => person.Rank % groupCount);
        Probe(ExpressionsGroupCount, key => groupBy.FirstOrDefault(grouping => grouping.Key == key) is { } found ? [.. found] : [], "this library's grouped query");
    }

    void SetupSource()
    {
        source = BenchmarkPerson.CreateCollection(ElementCount);
        stride = ElementCount / changeCount;
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
