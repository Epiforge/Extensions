namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Asks whether the cost of one key change through a live ordered view grows with the size of the collection, in each library, by holding the number of changes fixed and varying the number of elements
/// </summary>
/// <remarks>
/// <c>OrderedComparisonBenchmarks</c> found this library propagating an ordered key change in 1,339.4 ns against DynamicData's 989.0 ns at a thousand elements — the only arm in any comparison it loses — and found the shape of both figures consistent with an O(n) reposition within a list. This instrument exists to settle that, because a constant-factor problem and a complexity problem are worth very different amounts of attention before a release
/// </remarks>
/// <remarks>
/// <b>The workload is a thousand changes at every size, not one change per element.</b> Changing every element would make the total O(n squared) and the curve would grow whether the per-change cost did or not, which is the mistake this design exists to avoid. A fixed thousand changes reads the answer straight off the shape: flat means the per-change cost is O(1), linear means it is O(n)
/// </remarks>
/// <remarks>
/// The changed elements are spread evenly through the collection by stride, and each rank is exclusive-ored with 2 so that the element moves a couple of positions. <b>The distance moved is the same at every size</b>, so growth in the measured cost is structural rather than the workload quietly becoming harder
/// </remarks>
/// <remarks>
/// This instrument uses <see cref="ParamsAttribute" /> where the convention here is not to, for the same reason <c>ScaleComparisonBenchmarks</c> does: the element count is the question rather than a dimension of it
/// </remarks>
[MemoryDiagnoser]
public class OrderedScaleComparisonBenchmarks
{
    sealed class ByRank :
        IComparer<BenchmarkPerson>
    {
        internal static readonly ByRank Default = new();

        public int Compare(BenchmarkPerson? x, BenchmarkPerson? y) =>
            x!.Rank.CompareTo(y!.Rank);
    }

    const int changeCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, IComparable>> hoistedSelector = person => person.Rank;

    ReadOnlyObservableCollection<BenchmarkPerson> bound = null!;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IDisposable dynamicDataSubscription = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
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
    /// What a thousand rank changes cost before anything observes them, which is the floor both of the arms below stand on and which should itself be flat across the sizes
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

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksWithDynamicDataCache))]
    public void CleanupDynamicDataCache()
    {
        dynamicDataSubscription.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksWithExpressions))]
    public void CleanupExpressions()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }

    /// <summary>
    /// Moves one element to the end of the order and back again, so that an arrangement which is not actually reordering is caught here rather than reported as a fast one
    /// </summary>
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

    void SetupSource()
    {
        source = BenchmarkPerson.CreateCollection(ElementCount);
        stride = ElementCount / changeCount;
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksWithDynamicDataCache))]
    public void SetupStandingDynamicDataCache()
    {
        SetupSource();
        dynamicDataCache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        dynamicDataCache.AddOrUpdate(source);
        dynamicDataSubscription = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .SortAndBind(out bound, ByRank.Default)
            .Subscribe();
        Probe(() => bound, "DynamicData's sorted binding");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksWithExpressions))]
    public void SetupStandingExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        orderBy = sourceQuery.ObserveOrderBy(hoistedSelector);
        Probe(() => [.. orderBy], "this library's ordered query");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();

    void VerifyOrdered(IReadOnlyList<BenchmarkPerson> view, string arm)
    {
        if (view.Count != ElementCount)
            throw new InvalidOperationException($"{arm} presents {view.Count} elements where the source has {ElementCount}");
        for (var i = 1; i < view.Count; ++i)
            if (view[i - 1].Rank > view[i].Rank)
                throw new InvalidOperationException($"{arm} is not maintaining order at index {i}; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
    }
}
