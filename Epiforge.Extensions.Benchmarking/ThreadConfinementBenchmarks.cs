namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what an observer told that its observations are confined to one thread at a time saves over one which is not, on the shapes where making evaluation safe across threads cost the most
/// </summary>
/// <remarks>
/// Every arm runs under both settings of <see cref="ExpressionObserverOptions.IsThreadSafe"/>, and nothing else differs between the two runs of an arm. The unobserved arm reads the same under both and is there to show that the machine did too
/// </remarks>
[MemoryDiagnoser]
public class ThreadConfinementBenchmarks
{
    const int elementCount = 1000;
    const int observationCount = 1000;
    const int raises = 1000;

    static readonly PropertyInfo endProperty = typeof(BenchmarkTicker).GetProperty(nameof(BenchmarkTicker.End))!;

    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsPositive = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkTicker, bool>> watchesEnd = ticker => ticker.End > 0;

    /// <summary>
    /// Yields an expression watching the same property as every other and equal to none of them, so that the graph builds a comparison node for each
    /// </summary>
    static Expression<Func<BenchmarkTicker, bool>> WatchesEndDistinctly(int which)
    {
        var ticker = Expression.Parameter(typeof(BenchmarkTicker), "ticker");
        return Expression.Lambda<Func<BenchmarkTicker, bool>>(Expression.GreaterThan(Expression.MakeMemberAccess(ticker, endProperty), Expression.Constant((long)-which - 1)), ticker);
    }

    CollectionObserver collectionObserver = null!;
    IObservableExpression<BenchmarkTicker, bool>[] observations = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    BenchmarkTicker ticker = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [Params(true, false)]
    public bool IsThreadSafe { get; set; }

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    [Benchmark]
    public void ChangeEveryRankFilteredDirect() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankFilteredGraph() =>
        ChangeEveryRank();

    /// <summary>
    /// What changing every element's rank costs before anything observes it, which is the floor the filtered arms stand on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [GlobalCleanup(Targets = [nameof(RaiseUnderAThousandObservationsDirect), nameof(RaiseUnderAThousandObservationsGraph)])]
    public void CleanupObservations()
    {
        for (var i = 0; i < observationCount; ++i)
            observations[i].Dispose();
    }

    [GlobalCleanup(Targets = [nameof(ChangeEveryRankFilteredDirect), nameof(ChangeEveryRankFilteredGraph)])]
    public void CleanupStandingQuery()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    void ConstructAndDispose()
    {
        var query = collectionObserver.ObserveReadOnlyList(source);
        var filtered = query.ObserveWhere(rankIsPositive);
        filtered.Dispose();
        query.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeFilteredDirect() =>
        ConstructAndDispose();

    [Benchmark]
    public void ConstructAndDisposeFilteredGraph() =>
        ConstructAndDispose();

    ExpressionObserver CreateObserver(bool useDirectSubscription) =>
        new(new ExpressionObserverOptions { IsThreadSafe = IsThreadSafe, UseDirectSubscription = useDirectSubscription });

    void Raise()
    {
        for (var i = 0; i < raises; ++i)
            ticker.End = i + 1;
    }

    /// <summary>
    /// A raise against a thousand observations of one object, each attached to it apiece, so that the raise is a thousand evaluations and nothing else
    /// </summary>
    [Benchmark]
    public void RaiseUnderAThousandObservationsDirect() =>
        Raise();

    /// <summary>
    /// A raise against a thousand distinct comparisons of one property of one object, so that the raise is one member node feeding a thousand comparison nodes
    /// </summary>
    [Benchmark]
    public void RaiseUnderAThousandObservationsGraph() =>
        Raise();

    [GlobalSetup(Target = nameof(ConstructAndDisposeFilteredDirect))]
    public void SetupConstructDirect()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        collectionObserver = new CollectionObserver(CreateObserver(true));
    }

    [GlobalSetup(Target = nameof(ConstructAndDisposeFilteredGraph))]
    public void SetupConstructGraph()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        collectionObserver = new CollectionObserver(CreateObserver(false));
    }

    [GlobalSetup(Target = nameof(RaiseUnderAThousandObservationsDirect))]
    public void SetupRaiseDirect()
    {
        var observer = CreateObserver(true);
        ticker = new BenchmarkTicker();
        observations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        for (var i = 0; i < observationCount; ++i)
            observations[i] = observer.Observe(watchesEnd, ticker);
    }

    [GlobalSetup(Target = nameof(RaiseUnderAThousandObservationsGraph))]
    public void SetupRaiseGraph()
    {
        var observer = CreateObserver(false);
        ticker = new BenchmarkTicker();
        observations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        for (var i = 0; i < observationCount; ++i)
            observations[i] = observer.Observe(WatchesEndDistinctly(i), ticker);
    }

    void SetupStandingQuery(bool useDirectSubscription)
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        collectionObserver = new CollectionObserver(CreateObserver(useDirectSubscription));
        sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(rankIsPositive);
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankFilteredDirect))]
    public void SetupStandingQueryDirect() =>
        SetupStandingQuery(true);

    [GlobalSetup(Target = nameof(ChangeEveryRankFilteredGraph))]
    public void SetupStandingQueryGraph() =>
        SetupStandingQuery(false);

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        source = BenchmarkPerson.CreateCollection(elementCount);
}
