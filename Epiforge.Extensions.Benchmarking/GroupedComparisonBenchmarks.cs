namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using NMF.Expressions;
using NMF.Expressions.Linq;
using ObservableComputations;
using System.Reactive.Linq;

/// <summary>
/// Compares a live grouped view of a collection built with this library against ones built with DynamicData, NMF Expressions and ObservableComputations, on standing it up and on propagating a group key change to every element through it
/// </summary>
/// <remarks>
/// DynamicData is given <c>Group</c> over an <c>AutoRefresh</c> rather than <c>GroupOnProperty</c>, because the key here is a projection of a property rather than the property itself and <c>GroupOnProperty</c> takes a property selector. The <c>Refresh</c> case of its grouper moves an element between groups when the projected key changes, which is what this workload asks of it
/// </remarks>
/// <remarks>
/// Its result is materialized with <c>AsObservableCache</c>, because <c>Group</c> returns a stream of group change sets while this library's <c>ObserveGroupBy</c> returns a collection of groupings which can be read. Materializing is the cost of having the same thing to hold, so it belongs to the comparison rather than beside it
/// </remarks>
/// <remarks>
/// The workload moves every element between groups. Ranks are exclusive-ored with 2 and the key is the rank modulo sixteen, so every one of the thousand elements leaves a group and joins another on every round. That is the membership-churning workload the filtered comparison did not have, and it is deliberately the hardest thing either library is asked to do here
/// </remarks>
/// <remarks>
/// Each standing arm is verified before it is measured, because an arrangement which quietly failed to regroup would be reported as a very fast one. The probe moves an element's key and requires the element to be found in its new group and absent from its old
/// </remarks>
/// <remarks>
/// NMF Expressions and ObservableComputations are each measured in the form their documentation leads with, and both hold their groups as collections which can be read, so neither needs anything materialized. NMF's view is built over <c>WithUpdates</c> and given a dummy successor, because an NMF view with nothing attached to it does not follow its source; ObservableComputations' view is bound to an <c>OcConsumer</c>, whose disposal tears it down
/// </remarks>
/// <remarks>
/// A lookup built with <c>ObserveToLookup</c> keeps its groups the way a grouping does, so its arm moves the same elements through the same groups
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class GroupedComparisonBenchmarks
{
    const int elementCount = 1000;
    const int groupCount = 16;

    static readonly Expression<Func<BenchmarkPerson, int>> hoistedKeySelector = person => person.Rank % groupCount;

    IObservableCollectionQuery<IObservableGrouping<int, BenchmarkPerson>> groupBy = null!;
    IObservableLookupQuery<int, BenchmarkPerson> lookup = null!;
    INotifyEnumerable<INotifyGrouping<int, BenchmarkPerson>> nmfGroupBy = null!;
    OcConsumer observableComputationsConsumer = null!;
    Grouping<BenchmarkPerson, int> observableComputationsGrouping = null!;
    SourceCache<BenchmarkPerson, string> dynamicDataCache = null!;
    IObservableCache<IGroup<BenchmarkPerson, string, int>, int> dynamicDataGroups = null!;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    /// <summary>
    /// What changing every element's rank costs before anything observes it, which is the floor every arm below stands on
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
    public void ChangeEveryRankWithExpressionsLookup() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithNmf() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithObservableComputations() =>
        ChangeEveryRank();

    [Benchmark]
    public void ConstructAndDisposeWithDynamicDataCache()
    {
        var groups = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Group(person => person.Rank % groupCount)
            .AsObservableCache();
        groups.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithExpressions()
    {
        var query = observer.ObserveReadOnlyList(source);
        var grouped = query.ObserveGroupBy(hoistedKeySelector);
        grouped.Dispose();
        query.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithNmf()
    {
        var grouped = ((IEnumerable<BenchmarkPerson>)source).WithUpdates().GroupBy(hoistedKeySelector);
        grouped.Successors.SetDummy();
        grouped.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithObservableComputations()
    {
        var consumer = new OcConsumer();
        source.Grouping(hoistedKeySelector).For(consumer);
        consumer.Dispose();
    }

    [GlobalCleanup(Targets = [nameof(ChangeEveryRankWithDynamicDataCache), nameof(ConstructAndDisposeWithDynamicDataCache)])]
    public void CleanupDynamicDataCache()
    {
        dynamicDataGroups?.Dispose();
        dynamicDataCache.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void CleanupStandingExpressions()
    {
        groupBy.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressionsLookup))]
    public void CleanupStandingExpressionsLookup()
    {
        lookup.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithNmf))]
    public void CleanupStandingNmf() =>
        nmfGroupBy.Dispose();

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithObservableComputations))]
    public void CleanupStandingObservableComputations() =>
        observableComputationsConsumer.Dispose();

    IReadOnlyCollection<BenchmarkPerson> NmfGroup(int key) =>
        Enumerable.FirstOrDefault(nmfGroupBy, grouping => grouping.Key == key) is { } found ? [.. found] : [];

    IReadOnlyCollection<BenchmarkPerson> ObservableComputationsGroup(int key) =>
        Enumerable.FirstOrDefault(observableComputationsGrouping, group => group.Key == key) is { } found ? found : [];

    /// <summary>
    /// Moves one element between groups and back again, so that an arrangement which is not actually regrouping is caught here rather than reported as a fast one
    /// </summary>
    void Probe(Func<int, IReadOnlyCollection<BenchmarkPerson>> group, string arm)
    {
        VerifyPartitioned(group, arm);
        var subject = source[0];
        var held = subject.Rank;
        var wasIn = held % groupCount;
        var goesTo = (wasIn + 1) % groupCount;
        subject.Rank = held + 1;
        if (!group(goesTo).Contains(subject))
            throw new InvalidOperationException($"{arm} did not move an element into the group its key changed to; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
        if (group(wasIn).Contains(subject))
            throw new InvalidOperationException($"{arm} left an element in the group its key changed away from");
        subject.Rank = held;
        VerifyPartitioned(group, arm);
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

    [GlobalSetup(Targets = [nameof(ConstructAndDisposeWithNmf), nameof(ConstructAndDisposeWithObservableComputations)])]
    public void SetupConstructPeers() =>
        SetupSource();

    void SetupSource() =>
        source = BenchmarkPerson.CreateCollection(elementCount);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithDynamicDataCache))]
    public void SetupStandingDynamicDataCache()
    {
        SetupCache();
        dynamicDataGroups = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Group(person => person.Rank % groupCount)
            .AsObservableCache();
        Probe(key => dynamicDataGroups.Lookup(key) is { HasValue: true } found ? [.. found.Value.Cache.Items] : [], "DynamicData's grouping");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void SetupStandingExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        groupBy = sourceQuery.ObserveGroupBy(hoistedKeySelector);
        Probe(key => groupBy.FirstOrDefault(grouping => grouping.Key == key) is { } found ? [.. found] : [], "this library's grouped query");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressionsLookup))]
    public void SetupStandingExpressionsLookup()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        lookup = sourceQuery.ObserveToLookup(hoistedKeySelector);
        Probe(key => lookup.TryGetValue(key, out var found) ? [.. found] : [], "this library's lookup");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithNmf))]
    public void SetupStandingNmf()
    {
        SetupSource();
        nmfGroupBy = ((IEnumerable<BenchmarkPerson>)source).WithUpdates().GroupBy(hoistedKeySelector);
        nmfGroupBy.Successors.SetDummy();
        Probe(NmfGroup, "NMF's grouping");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithObservableComputations))]
    public void SetupStandingObservableComputations()
    {
        SetupSource();
        observableComputationsConsumer = new OcConsumer();
        observableComputationsGrouping = source.Grouping(hoistedKeySelector).For(observableComputationsConsumer);
        Probe(ObservableComputationsGroup, "ObservableComputations' grouping");
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();

    static void VerifyPartitioned(Func<int, IReadOnlyCollection<BenchmarkPerson>> group, string arm)
    {
        var total = 0;
        for (var key = 0; key < groupCount; ++key)
            total += group(key).Count;
        if (total != elementCount)
            throw new InvalidOperationException($"{arm} accounts for {total} elements across its groups where the source has {elementCount}");
    }
}
