namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using NMF.Expressions;
using NMF.Expressions.Linq;
using ObservableComputations;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Compares what a property change costs a live filtered view when the change moves the element into or out of the view, in this library, DynamicData, NMF Expressions and ObservableComputations
/// </summary>
/// <remarks>
/// Every element's rank is negated once per operation, so every element leaves the view on one operation and returns on the next. The other comparison classes change ranks without moving anything, which is the cheaper case for every library; this one prices the case in which the view itself changes and says so. This library's view raises no event when nothing handles its events, while the others' views are collections which raise theirs regardless, so this library is measured both without a handler and with one which does nothing. DynamicData is measured on its cache, the shape its operators are built around. Every standing view is verified before it is measured
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class MembershipChangeComparisonBenchmarks
{
    static readonly Expression<Func<BenchmarkPerson, bool>> hoistedPredicate = person => person.Rank > 0;
    static readonly NotifyCollectionChangedEventHandler ignore = (sender, e) => { };

    SourceCache<BenchmarkPerson, string>? dynamicDataCache;
    ReadOnlyObservableCollection<BenchmarkPerson> dynamicDataResults = null!;
    IDisposable? dynamicDataSubscription;
    INotifyEnumerable<BenchmarkPerson>? nmfWhere;
    OcConsumer? observableComputationsConsumer;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson>? sourceQuery;
    IObservableCollectionQuery<BenchmarkPerson>? where;

    [Params(1000, 10000)]
    public int ElementCount { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (where is not null)
            where.CollectionChanged -= ignore;
        where?.Dispose();
        where = null;
        sourceQuery?.Dispose();
        sourceQuery = null;
        dynamicDataSubscription?.Dispose();
        dynamicDataSubscription = null;
        dynamicDataCache?.Dispose();
        dynamicDataCache = null;
        nmfWhere?.Dispose();
        nmfWhere = null;
        observableComputationsConsumer?.Dispose();
        observableComputationsConsumer = null;
    }

    void FlipEveryRank()
    {
        for (var i = 0; i < ElementCount; ++i)
        {
            var person = source[i];
            person.Rank = -person.Rank;
        }
    }

    /// <summary>
    /// What negating every element's rank costs before anything observes it, which is the floor every arm below stands on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void FlipEveryRankUnobserved() =>
        FlipEveryRank();

    [Benchmark]
    public void FlipEveryRankWithDynamicDataCache() =>
        FlipEveryRank();

    [Benchmark]
    public void FlipEveryRankWithExpressions() =>
        FlipEveryRank();

    [Benchmark]
    public void FlipEveryRankWithExpressionsHandled() =>
        FlipEveryRank();

    [Benchmark]
    public void FlipEveryRankWithNmf() =>
        FlipEveryRank();

    [Benchmark]
    public void FlipEveryRankWithObservableComputations() =>
        FlipEveryRank();

    /// <summary>
    /// Moves every element out of the view and back in, so that a view which is not actually following its source is caught here rather than reported as a fast one
    /// </summary>
    void Probe(Func<int> count, string arm)
    {
        if (count() != ElementCount)
            throw new InvalidOperationException($"{arm} does not hold every element its predicate admits");
        FlipEveryRank();
        if (count() != 0)
            throw new InvalidOperationException($"{arm} kept elements its predicate stopped admitting");
        FlipEveryRank();
        if (count() != ElementCount)
            throw new InvalidOperationException($"{arm} did not take back every element its predicate admits again");
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithDynamicDataCache))]
    public void SetupDynamicDataCache()
    {
        SetupSource();
        dynamicDataCache = new SourceCache<BenchmarkPerson, string>(person => person.Name);
        dynamicDataCache.AddOrUpdate(source);
        dynamicDataSubscription = dynamicDataCache
            .Connect()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank > 0)
            .Bind(out dynamicDataResults)
            .Subscribe();
        Probe(() => dynamicDataResults.Count, "DynamicData's cache");
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithExpressions))]
    public void SetupExpressions() =>
        SetupExpressions(false);

    void SetupExpressions(bool handled)
    {
        SetupSource();
        sourceQuery = new CollectionObserver().ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(hoistedPredicate);
        if (handled)
            where.CollectionChanged += ignore;
        Probe(() => where.Count, "this library's filter");
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithExpressionsHandled))]
    public void SetupExpressionsHandled() =>
        SetupExpressions(true);

    [GlobalSetup(Target = nameof(FlipEveryRankWithNmf))]
    public void SetupNmf()
    {
        SetupSource();
        nmfWhere = ((IEnumerable<BenchmarkPerson>)source).WithUpdates().Where(hoistedPredicate);
        nmfWhere.Successors.SetDummy();
        Probe(() => nmfWhere.Count(), "NMF's filter");
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithObservableComputations))]
    public void SetupObservableComputations()
    {
        SetupSource();
        observableComputationsConsumer = new OcConsumer();
        var filtering = source.Filtering(hoistedPredicate).For(observableComputationsConsumer);
        Probe(() => filtering.Count, "ObservableComputations' filter");
    }

    void SetupSource()
    {
        var people = new List<BenchmarkPerson>(ElementCount);
        for (var i = 0; i < ElementCount; ++i)
            people.Add(new BenchmarkPerson($"P{i}", i + 1));
        source = new ObservableRangeCollection<BenchmarkPerson>(people);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
