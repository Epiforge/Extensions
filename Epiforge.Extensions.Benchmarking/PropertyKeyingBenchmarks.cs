namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices both sides of keying a direct subscription by property name: the fast path places one handler on an object for each property an expression reads where the graph places one for the object, which costs more to build and may cost less to notify
/// </summary>
[MemoryDiagnoser]
public class PropertyKeyingBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> oneProperty = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> twoProperties = person => person.Rank > 0 & person.Name.Length > 0;

    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> standing = null!;
    IObservableCollectionQuery<BenchmarkPerson> standingSource = null!;

    /// <summary>
    /// Changes the one property both mechanisms subscribe to, once for every element, with nothing observing it, which is what the changed objects raise before either mechanism sees anything
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithTwoPropertiesObservedDirect() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithTwoPropertiesObservedGraph() =>
        ChangeEveryRank();

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    void ConstructAndDispose(Expression<Func<BenchmarkPerson, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void OnePropertyConstructDirect() =>
        ConstructAndDispose(oneProperty);

    [Benchmark]
    public void TwoPropertiesConstructDirect() =>
        ConstructAndDispose(twoProperties);

    [Benchmark]
    public void TwoPropertiesConstructGraph() =>
        ConstructAndDispose(twoProperties);

    void SetupObserver(bool useDirectSubscription)
    {
        observer = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription }));
        source = BenchmarkPerson.CreateCollection(elementCount);
    }

    void SetupStanding(bool useDirectSubscription)
    {
        SetupObserver(useDirectSubscription);
        standingSource = observer.ObserveReadOnlyList(source);
        standing = standingSource.ObserveWhere(twoProperties);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupObserver(true);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithTwoPropertiesObservedDirect))]
    public void SetupStandingDirect() =>
        SetupStanding(true);

    [GlobalSetup(Target = nameof(ChangeEveryRankWithTwoPropertiesObservedGraph))]
    public void SetupStandingGraph() =>
        SetupStanding(false);

    [GlobalSetup(Targets = [nameof(OnePropertyConstructDirect), nameof(TwoPropertiesConstructDirect)])]
    public void SetupConstructDirect() =>
        SetupObserver(true);

    [GlobalSetup(Target = nameof(TwoPropertiesConstructGraph))]
    public void SetupConstructGraph() =>
        SetupObserver(false);

    [GlobalCleanup(Targets = [nameof(ChangeEveryRankWithTwoPropertiesObservedDirect), nameof(ChangeEveryRankWithTwoPropertiesObservedGraph)])]
    public void CleanupStanding()
    {
        standing.Dispose();
        standingSource.Dispose();
    }
}
