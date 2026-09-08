namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Asks how the cost of an ordered key change depends on how far the element has to move, in each library
/// </summary>
/// <remarks>
/// This exists to keep an optimization honest. <c>OrderedScaleComparisonBenchmarks</c> moves every element two positions, and the change under consideration for the ordered query — searching for the destination from the node it already holds rather than from the root of the sequence — is at its best over exactly that distance. Measuring only short moves would tune the library to the harness. These arms establish what short moves, middling moves and long moves each cost before anything is changed, so the after can be judged on all three
/// </remarks>
/// <remarks>
/// The rank is exclusive-ored with the distance rather than added to it, for two reasons: the operation is its own inverse, so the collection returns to its starting arrangement on every second pass and the measurement does not drift; and with a power of two it toggles a single bit, so the element moves that many positions and no other element's key is disturbed
/// </remarks>
/// <remarks>
/// DynamicData is measured alongside, not because its result is in question but because it establishes whether this library's loss at a thousand elements holds at every move distance or only at short ones, which is a different sentence for a reader either way
/// </remarks>
[MemoryDiagnoser]
public class OrderByMoveDistanceBenchmarks
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

    ReadOnlyObservableCollection<BenchmarkPerson> bound = null!;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IDisposable dynamicDataSubscription = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    /// <summary>
    /// How far each element moves, as the single bit of its rank which is toggled
    /// </summary>
    [Params(2, 32, 512)]
    public int MoveDistance { get; set; }

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= MoveDistance;
    }

    /// <summary>
    /// What the same changes cost before anything observes them, which is the floor both of the arms below stand on and which should be the same at every distance
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

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithDynamicDataCache))]
    public void CleanupDynamicDataCache()
    {
        dynamicDataSubscription.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
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

    void SetupSource() =>
        source = BenchmarkPerson.CreateCollection(elementCount);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithDynamicDataCache))]
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

    static void VerifyOrdered(IReadOnlyList<BenchmarkPerson> view, string arm)
    {
        if (view.Count != elementCount)
            throw new InvalidOperationException($"{arm} presents {view.Count} elements where the source has {elementCount}");
        for (var i = 1; i < view.Count; ++i)
            if (view[i - 1].Rank > view[i].Rank)
                throw new InvalidOperationException($"{arm} is not maintaining order at index {i}; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
    }
}
