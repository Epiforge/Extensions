namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Asks whether either library's standing gains or loses when one operator is built on another, by ordering and by grouping a filtered view rather than the whole collection
/// </summary>
/// <remarks>
/// Every comparison run before this one measured a single operator, which is not how either library is used. Two things could change under composition and they point in opposite directions. <b>A filter shrinks the collection the next operator sees</b>, and since this library's ordered reposition costs a large fixed amount and a small amount per element while DynamicData's costs the reverse, shrinking the collection moves their crossover — the sorted view here is the filtered one, not the source. <b>Against that, every stage of a DynamicData pipeline materializes a change set</b> where this library's filter stays silent for a change which does not alter membership, so a longer chain may cost them more per change and this library nothing
/// </remarks>
/// <remarks>
/// The source holds ten thousand elements and the filter admits either a tenth or half of them, which puts the ordered view on either side of the ~1,400 element crossover measured by <c>OrderedScaleComparisonBenchmarks</c>. Both libraries are asked for a filter which depends on the property that changes, so both must re-check membership on every change; DynamicData is therefore given <c>AutoRefresh</c> before <c>Filter</c> rather than after, which is what a correct pipeline needs when the predicate reads the refreshed property
/// </remarks>
/// <remarks>
/// A thousand changes are made at every setting, spread by stride through the admitted range, and each rank is exclusive-ored with 2 so that no element ever crosses the filter boundary. <b>Membership therefore never changes and the ordered and grouped stages downstream always do work</b>, which is the case that separates the two designs; a workload which changed membership would be measuring the filter's own comparison over again
/// </remarks>
/// <remarks>
/// The arms which stop at the filter are not controls but subtrahends: subtracting them from the arms which continue prices what ordering or grouping costs <i>on top of</i> a filter, which is the question, rather than what it costs alone, which is already known
/// </remarks>
[MemoryDiagnoser]
public class ChainedComparisonBenchmarks
{
    sealed class ByRank :
        IComparer<BenchmarkPerson>
    {
        internal static readonly ByRank Default = new();

        public int Compare(BenchmarkPerson? x, BenchmarkPerson? y) =>
            x!.Rank.CompareTo(y!.Rank);
    }

    const int changeCount = 1000;
    const int elementCount = 10_000;
    const int groupCount = 16;

    ReadOnlyObservableCollection<BenchmarkPerson> bound = null!;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IObservableCache<IGroup<BenchmarkPerson, string, int>, int> dynamicDataGroups = null!;
    IDisposable dynamicDataSubscription = null!;
    IObservableCollectionQuery<IObservableGrouping<int, BenchmarkPerson>> groupBy = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    int stride;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    /// <summary>
    /// How many of the ten thousand elements the filter admits, which is the size of the collection the ordered and grouped stages actually see
    /// </summary>
    [Params(1_000, 5_000)]
    public int AdmittedCount { get; set; }

    void ChangeAThousandRanks()
    {
        for (var i = 0; i < changeCount; ++i)
            source[i * stride].Rank ^= 2;
    }

    /// <summary>
    /// What the changes cost before anything observes them, which is the floor every arm below stands on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeAThousandRanksUnobserved() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredWithDynamicData() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredWithExpressions() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredThenGroupedWithDynamicData() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredThenGroupedWithExpressions() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredThenOrderedWithDynamicData() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksFilteredThenOrderedWithExpressions() =>
        ChangeAThousandRanks();

    [GlobalCleanup(Targets = [nameof(ChangeAThousandRanksFilteredWithDynamicData), nameof(ChangeAThousandRanksFilteredThenOrderedWithDynamicData)])]
    public void CleanupDynamicData()
    {
        dynamicDataSubscription.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksFilteredThenGroupedWithDynamicData))]
    public void CleanupDynamicDataGrouped()
    {
        dynamicDataGroups.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksFilteredWithExpressions))]
    public void CleanupExpressionsFiltered()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksFilteredThenGroupedWithExpressions))]
    public void CleanupExpressionsGrouped()
    {
        groupBy.Dispose();
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksFilteredThenOrderedWithExpressions))]
    public void CleanupExpressionsOrdered()
    {
        orderBy.Dispose();
        where.Dispose();
        sourceQuery.Dispose();
    }

    IObservable<IChangeSet<BenchmarkPerson, string>> DynamicDataFiltered() =>
        dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank < AdmittedCount);

    /// <summary>
    /// Requires the filtered view to hold what the filter admits, so that an arrangement which quietly admits everything is caught here rather than reported as a fast one
    /// </summary>
    void VerifyAdmitted(int count, string arm)
    {
        if (count != AdmittedCount)
            throw new InvalidOperationException($"{arm} holds {count} elements where the filter admits {AdmittedCount}");
    }

    /// <summary>
    /// Requires the grouped view to account for every admitted element across its groups
    /// </summary>
    void VerifyGrouped(Func<int, int> countInGroup, string arm)
    {
        var total = 0;
        for (var key = 0; key < groupCount; ++key)
            total += countInGroup(key);
        VerifyAdmitted(total, arm);
    }

    /// <summary>
    /// Requires the ordered view to be in order and to hold what the filter admits, and to reorder when a key changes
    /// </summary>
    void VerifyOrdered(Func<IReadOnlyList<BenchmarkPerson>> view, string arm)
    {
        var ordered = view();
        VerifyAdmitted(ordered.Count, arm);
        for (var i = 1; i < ordered.Count; ++i)
            if (ordered[i - 1].Rank > ordered[i].Rank)
                throw new InvalidOperationException($"{arm} is not maintaining order at index {i}; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
        var subject = source[0];
        var held = subject.Rank;
        subject.Rank = AdmittedCount - 1;
        var moved = view();
        if (!ReferenceEquals(moved[moved.Count - 1], subject))
            throw new InvalidOperationException($"{arm} did not move an element whose key became the largest admitted to the end of the order");
        subject.Rank = held;
    }

    void SetupCache()
    {
        SetupSource();
        dynamicDataCache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        dynamicDataCache.AddOrUpdate(source);
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredWithDynamicData))]
    public void SetupDynamicDataFiltered()
    {
        SetupCache();
        dynamicDataSubscription = DynamicDataFiltered().Bind(out bound).Subscribe();
        VerifyAdmitted(bound.Count, "DynamicData's filtered binding");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredThenGroupedWithDynamicData))]
    public void SetupDynamicDataFilteredThenGrouped()
    {
        SetupCache();
        dynamicDataGroups = DynamicDataFiltered().Group(person => person.Rank % groupCount).AsObservableCache();
        VerifyGrouped(key => dynamicDataGroups.Lookup(key) is { HasValue: true } found ? found.Value.Cache.Count : 0, "DynamicData's grouping over a filter");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredThenOrderedWithDynamicData))]
    public void SetupDynamicDataFilteredThenOrdered()
    {
        SetupCache();
        dynamicDataSubscription = DynamicDataFiltered().SortAndBind(out bound, ByRank.Default).Subscribe();
        VerifyOrdered(() => bound, "DynamicData's sorted binding over a filter");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredWithExpressions))]
    public void SetupExpressionsFiltered()
    {
        SetupWhere();
        VerifyAdmitted(where.Count, "this library's filtered query");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredThenGroupedWithExpressions))]
    public void SetupExpressionsFilteredThenGrouped()
    {
        SetupWhere();
        groupBy = where.ObserveGroupBy(person => person.Rank % groupCount);
        VerifyGrouped(key => groupBy.FirstOrDefault(grouping => grouping.Key == key) is { } found ? found.Count : 0, "this library's grouping over a filter");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksFilteredThenOrderedWithExpressions))]
    public void SetupExpressionsFilteredThenOrdered()
    {
        SetupWhere();
        orderBy = where.ObserveOrderBy(person => person.Rank);
        VerifyOrdered(() => [.. orderBy], "this library's ordered query over a filter");
    }

    void SetupSource()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        stride = AdmittedCount / changeCount;
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();

    void SetupWhere()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(person => person.Rank < AdmittedCount);
    }
}
