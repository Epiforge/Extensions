namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what an observation costs where the runtime cannot generate code, and what the fast path's two-parameter evaluation costs where it can, by running the same arms against this repository with and without dynamic code and against the last release
/// </summary>
/// <remarks>
/// Each arm works over a thousand people. The changing arms flip a bit of every person's rank, which moves some across the filter, through a view on the fast path and through one on the graph; the raising arm changes one person observed a thousand times, so it is a thousand evaluations and nothing else; the building arm builds and disposes a filtered view, which is where compiling and invoking by reflection are paid. The unobserved arm is the floor every changing arm stands on
/// </remarks>
[AgainstReleasedExpressions("7.0.4")]
[MemoryDiagnoser]
[WithoutDynamicCode]
public class DynamicCodeBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsPositive = person => person.Rank > 0;

    readonly List<IDisposable> standing = [];
    BenchmarkPerson raised = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    CollectionObserver collectionObserver = null!;

    [Benchmark]
    public void BuildAndDisposeAFilteredView()
    {
        var observation = collectionObserver.ObserveReadOnlyList(source);
        var filtered = observation.ObserveWhere(rankIsPositive);
        filtered.Dispose();
        observation.Dispose();
    }

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    [Benchmark]
    public void ChangeEveryRankThroughAFilteredView() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankThroughAFilteredViewOnTheGraph() =>
        ChangeEveryRank();

    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark]
    public void RaiseUnderAThousandObservations() =>
        raised.Rank ^= 2;

    [GlobalSetup(Target = nameof(BuildAndDisposeAFilteredView))]
    public void SetupBuilding()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        collectionObserver = new CollectionObserver();
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankThroughAFilteredView))]
    public void SetupFilteredView() =>
        StandFilteredView(new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true })));

    [GlobalSetup(Target = nameof(ChangeEveryRankThroughAFilteredViewOnTheGraph))]
    public void SetupFilteredViewOnTheGraph() =>
        StandFilteredView(new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false })));

    [GlobalSetup(Target = nameof(RaiseUnderAThousandObservations))]
    public void SetupRaising()
    {
        raised = new BenchmarkPerson("raised", 1);
        var expressionObserver = new ExpressionObserver();
        for (var i = 0; i < elementCount; ++i)
            standing.Add(expressionObserver.Observe(rankIsPositive, raised));
        if (expressionObserver.CachedObservableExpressions != 0)
            throw new InvalidOperationException("the raising arm's observations did not take the fast path");
        var raisedObservation = (IObservableExpression<BenchmarkPerson, bool>)standing[0];
        raised.Rank = -1;
        if (raisedObservation.Evaluation.Result)
            throw new InvalidOperationException("the raising arm's observations do not follow the rank");
        raised.Rank = 1;
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        source = BenchmarkPerson.CreateCollection(elementCount);

    void StandFilteredView(CollectionObserver observer)
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        var observation = observer.ObserveReadOnlyList(source);
        standing.Add(observation);
        var filtered = observation.ObserveWhere(rankIsPositive);
        standing.Add(filtered);
        var before = filtered.Count;
        source[0].Rank = 5;
        if (filtered.Count != before + 1)
            throw new InvalidOperationException("the filtered view does not follow the ranks");
        source[0].Rank = 0;
    }
}
