namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Compares a live ordered view of a collection built with this library against one built with DynamicData, on standing it up and on propagating a key change to every element through it
/// </summary>
/// <remarks>
/// DynamicData's <c>Sort</c> is marked obsolete in 9.4.33 in favour of <c>SortAndBind</c>, so <c>SortAndBind</c> is what is measured here. Using the obsolete operator would have measured a path its authors have already moved away from
/// </remarks>
/// <remarks>
/// The workload moves every element. Ranks are exclusive-ored with 2, so each element trades places with the one two positions away and a thousand repositions are required of each library on every round. This is deliberately harder than the filtered comparison, where only two elements of a thousand crossed the predicate: ordering is ground DynamicData is built for, and a workload which left the order alone would not be a comparison at all
/// </remarks>
/// <remarks>
/// Each standing arm is verified before it is measured. An instrument which stood up an arrangement that quietly failed to reorder would report that arrangement as very fast, so <see cref="VerifyOrdered(IReadOnlyList{BenchmarkPerson}, string)" /> is run against a probe change in setup and throws rather than allowing a no-op to be published as a result
/// </remarks>
[MemoryDiagnoser]
public class OrderedComparisonBenchmarks
{
    sealed class ByRank :
        IComparer<BenchmarkPerson>
    {
        internal static readonly ByRank Default = new();

        public int Compare(BenchmarkPerson? x, BenchmarkPerson? y) =>
            x!.Rank.CompareTo(y!.Rank);
    }

    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, IComparable>> hoistedSelector = person => person.Rank;

    static void VerifyOrdered(IReadOnlyList<BenchmarkPerson> view, string arm)
    {
        if (view.Count != elementCount)
            throw new InvalidOperationException($"{arm} presents {view.Count} elements where the source has {elementCount}");
        for (var i = 1; i < view.Count; ++i)
            if (view[i - 1].Rank > view[i].Rank)
                throw new InvalidOperationException($"{arm} is not maintaining order at index {i}; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
    }

    ReadOnlyObservableCollection<BenchmarkPerson> bound = null!;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IDisposable dynamicDataSubscription = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    /// <summary>
    /// What changing every element's rank costs before anything observes it, which is the floor both of the arms below stand on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithDynamicDataCache() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithExpressions() =>
        ChangeEveryRank();

    [Benchmark]
    public void ConstructAndDisposeWithDynamicDataCache()
    {
        var subscription = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .SortAndBind(out var sorted, ByRank.Default)
            .Subscribe();
        subscription.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithExpressions()
    {
        var query = observer.ObserveReadOnlyList(source);
        var ordered = query.ObserveOrderBy(hoistedSelector);
        ordered.Dispose();
        query.Dispose();
    }

    [GlobalCleanup(Targets = [nameof(ChangeEveryRankWithDynamicDataCache), nameof(ConstructAndDisposeWithDynamicDataCache)])]
    public void CleanupDynamicDataCache()
    {
        dynamicDataSubscription?.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void CleanupStandingExpressions()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }

    /// <summary>
    /// Moves one element to the end of the order and back again, so that an arrangement which is not actually reordering is caught here rather than reported as a fast one
    /// </summary>
    /// <remarks>
    /// The view is taken afresh at each check rather than held, because the two libraries present their results through different types and a snapshot taken before the change would answer the question the probe is asking with the state that preceded it
    /// </remarks>
    void Probe(Func<IReadOnlyList<BenchmarkPerson>> view, string arm)
    {
        VerifyOrdered(view(), arm);
        var subject = source[0];
        var held = subject.Rank;
        subject.Rank = int.MaxValue;
        var moved = view();
        if (!ReferenceEquals(moved[moved.Count - 1], subject))
            throw new InvalidOperationException($"{arm} did not move an element whose key became the largest to the end of the order");
        subject.Rank = held;
        VerifyOrdered(view(), arm);
    }

    void SetupCache()
    {
        SetupSource();
        dynamicDataCache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        dynamicDataCache.AddOrUpdate(source);
    }

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithDynamicDataCache))]
    public void SetupConstructDynamicDataCache() =>
        SetupCache();

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithExpressions))]
    public void SetupConstructExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
    }

    void SetupSource() =>
        source = BenchmarkPerson.CreateCollection(elementCount);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithDynamicDataCache))]
    public void SetupStandingDynamicDataCache()
    {
        SetupCache();
        dynamicDataSubscription = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .SortAndBind(out bound, ByRank.Default)
            .Subscribe();
        Probe(() => bound, "DynamicData's sorted binding");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void SetupStandingExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        orderBy = sourceQuery.ObserveOrderBy(hoistedSelector);
        Probe(() => [.. orderBy], "this library's ordered query");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
