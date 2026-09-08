namespace Epiforge.Extensions.Benchmarking;

using DynamicData;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

/// <summary>
/// Compares a live filtered view of a collection built with this library against one built with DynamicData, on the two things such a view costs: standing it up, and propagating a change to every element through it
/// </summary>
/// <remarks>
/// The two are asked for the same behavior and not for the same code. This library reads the dependency out of the predicate; DynamicData is told it by <c>AutoRefresh</c>, which is a difference in what the caller writes rather than in what the result does. Both bind their results into a collection which stays correct as ranks change, and the unobserved arm is what those changes cost before either of them sees anything.
/// </remarks>
/// <remarks>
/// Construction is measured twice for this library, because the two figures are honest about different callers. A predicate written once and reused is analyzed and compiled once, which is what the query layer itself does and what a caller who hoists their expression gets; a predicate written inline at every call builds a new expression tree every time and is analyzed and compiled every time. DynamicData's lambda is a delegate the compiler caches in a static field, so it has no equivalent of the second figure and none is invented for it.
/// </remarks>
[MemoryDiagnoser]
public class DynamicDataComparisonBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> hoistedPredicate = person => person.Rank > 0;

    IDisposable dynamicDataSubscription = null!;
    CollectionObserver observer = null!;
    ReadOnlyObservableCollection<BenchmarkPerson> dynamicDataResults = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

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
    public void ChangeEveryRankWithDynamicData() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithExpressions() =>
        ChangeEveryRank();

    [Benchmark]
    public void ConstructAndDisposeWithDynamicData()
    {
        var subscription = source
            .ToObservableChangeSet()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank > 0)
            .Bind(out var bound)
            .Subscribe();
        subscription.Dispose();
    }

    /// <summary>
    /// Builds the filtered query from a predicate written once and reused, so that the expression is analyzed and compiled on the first construction and taken from the cache thereafter
    /// </summary>
    [Benchmark]
    public void ConstructAndDisposeWithExpressions()
    {
        var query = observer.ObserveReadOnlyList(source);
        var filtered = query.ObserveWhere(hoistedPredicate);
        filtered.Dispose();
        query.Dispose();
    }

    /// <summary>
    /// Builds the same filtered query from a predicate written inline, so that a new expression tree is built, analyzed and compiled every time, which is what a caller who does not hoist their predicate pays
    /// </summary>
    [Benchmark]
    public void ConstructAndDisposeWithExpressionsFromAFreshPredicate()
    {
        var query = observer.ObserveReadOnlyList(source);
        var filtered = query.ObserveWhere(person => person.Rank > 0);
        filtered.Dispose();
        query.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithDynamicData))]
    public void CleanupStandingDynamicData() =>
        dynamicDataSubscription.Dispose();

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void CleanupStandingExpressions()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithDynamicData))]
    public void SetupConstructDynamicData() =>
        SetupSource();

    [GlobalSetup(Targets = [nameof(ConstructAndDisposeWithExpressions), nameof(ConstructAndDisposeWithExpressionsFromAFreshPredicate)])]
    public void SetupConstructExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
    }

    void SetupSource() =>
        source = BenchmarkPerson.CreateCollection(elementCount);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithDynamicData))]
    public void SetupStandingDynamicData()
    {
        SetupSource();
        dynamicDataSubscription = source
            .ToObservableChangeSet()
            .AutoRefresh(person => person.Rank)
            .Filter(person => person.Rank > 0)
            .Bind(out dynamicDataResults)
            .Subscribe();
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void SetupStandingExpressions()
    {
        SetupSource();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(hoistedPredicate);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
