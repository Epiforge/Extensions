namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using NMF.Expressions;
using NMF.Expressions.Linq;
using ObservableComputations;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Measures one key change through a live ordered view in each library when the element's new place is anywhere in the order, rather than two places from where it was
/// </summary>
/// <remarks>
/// <c>OrderedComparisonBenchmarks</c> and <c>OrderedScaleComparisonBenchmarks</c> both exclusive-or ranks with 2, so every element they change moves a couple of places. That is the cheapest move an ordered view can be asked for and it is the only one either measures. Here each change gives a randomly chosen element a randomly chosen rank from four times as many values as there are elements, so the distance moved is a fraction of the order that grows with its size, and the elements touched are scattered through memory
/// </remarks>
/// <remarks>
/// <b>The changes follow one schedule which every arm replays from its start.</b> It is drawn once from a fixed seed and is long enough that a run wraps it only after the ranks it assigned first have been replaced many times over, so replaying it still moves elements to new places. A rank equal to the element's own is raised by one, so every assignment is a change
/// </remarks>
/// <remarks>
/// This instrument uses <see cref="ParamsAttribute" /> where the convention here is not to, for the reason <c>OrderedScaleComparisonBenchmarks</c> does: the element count is the question rather than a dimension of it
/// </remarks>
/// <remarks>
/// NMF Expressions and ObservableComputations are measured as <c>OrderedComparisonBenchmarks</c> measures them, and every standing view is verified before it is measured, after the schedule's first thousand changes as well as before them
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class OrderedRandomMoveComparisonBenchmarks
{
    sealed class ByRank :
        IComparer<BenchmarkPerson>
    {
        internal static readonly ByRank Default = new();

        public int Compare(BenchmarkPerson? x, BenchmarkPerson? y) =>
            x!.Rank.CompareTo(y!.Rank);
    }

    const int changeCount = 1000;
    const int scheduleLength = 1 << 16;

    static readonly Expression<Func<BenchmarkPerson, int>> hoistedRank = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, IComparable>> hoistedSelector = person => person.Rank;

    ReadOnlyObservableCollection<BenchmarkPerson> bound = null!;
    int cursor;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IDisposable dynamicDataSubscription = null!;
    INotifyEnumerable<BenchmarkPerson> nmfOrderBy = null!;
    OcConsumer observableComputationsConsumer = null!;
    Ordering<BenchmarkPerson, int> observableComputationsOrdering = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    int[] scheduledElements = null!;
    int[] scheduledRanks = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    [Params(1_000, 10_000)]
    public int ElementCount { get; set; }

    void ChangeAThousandRanks()
    {
        for (var i = 0; i < changeCount; ++i)
        {
            var person = source[scheduledElements[cursor]];
            var rank = scheduledRanks[cursor];
            person.Rank = rank == person.Rank ? rank + 1 : rank;
            if (++cursor == scheduleLength)
                cursor = 0;
        }
    }

    /// <summary>
    /// What a thousand rank changes cost before anything observes them, which is the floor every arm below stands on
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
    public void ChangeAThousandRanksWithNmf() =>
        ChangeAThousandRanks();

    [Benchmark]
    public void ChangeAThousandRanksWithObservableComputations() =>
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

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksWithNmf))]
    public void CleanupNmf() =>
        nmfOrderBy.Dispose();

    [GlobalCleanup(Target = nameof(ChangeAThousandRanksWithObservableComputations))]
    public void CleanupObservableComputations() =>
        observableComputationsConsumer.Dispose();

    /// <summary>
    /// Moves one element to the end of the order and back again, then plays the schedule's first thousand changes and checks the order again before rewinding the schedule, so that an arrangement which is not actually reordering is caught here rather than reported as a fast one
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
        ChangeAThousandRanks();
        VerifyOrdered(view(), arm);
        cursor = 0;
    }

    void SetupSource()
    {
        source = BenchmarkPerson.CreateCollection(ElementCount);
        var random = new Random(20260928);
        scheduledElements = new int[scheduleLength];
        scheduledRanks = new int[scheduleLength];
        for (var i = 0; i < scheduleLength; ++i)
        {
            scheduledElements[i] = random.Next(ElementCount);
            scheduledRanks[i] = random.Next(ElementCount * 4);
        }
        cursor = 0;
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

    [GlobalSetup(Target = nameof(ChangeAThousandRanksWithNmf))]
    public void SetupStandingNmf()
    {
        SetupSource();
        nmfOrderBy = ((IEnumerable<BenchmarkPerson>)source).WithUpdates().OrderBy(hoistedRank);
        nmfOrderBy.Successors.SetDummy();
        Probe(() => [.. nmfOrderBy], "NMF's ordering");
    }

    [GlobalSetup(Target = nameof(ChangeAThousandRanksWithObservableComputations))]
    public void SetupStandingObservableComputations()
    {
        SetupSource();
        observableComputationsConsumer = new OcConsumer();
        observableComputationsOrdering = source.Ordering(hoistedRank).For(observableComputationsConsumer);
        Probe(() => observableComputationsOrdering, "ObservableComputations' ordering");
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
