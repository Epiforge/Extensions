namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObservationConstructionBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> constantPredicate = person => true;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    ExpressionObserver direct = null!;
    ExpressionObserver graph = null!;
    IObservableExpression<BenchmarkPersonWithPartner, bool>[] held = null!;
    BenchmarkPersonWithPartner[] people = null!;

    [Benchmark]
    public void ConstantDirect() =>
        ConstructAndDispose(direct, constantPredicate);

    [Benchmark]
    public void ConstantDirectHeld() =>
        ConstructHoldAndDispose(direct, constantPredicate);

    [Benchmark]
    public void ConstantGraph() =>
        ConstructAndDispose(graph, constantPredicate);

    [Benchmark]
    public void ConstantGraphHeld() =>
        ConstructHoldAndDispose(graph, constantPredicate);

    void ConstructAndDispose(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        for (var i = 0; i < elementCount; ++i)
            observer.Observe(predicate, people[i]).Dispose();
    }

    /// <summary>
    /// Constructs every observation before disposing any of them, which is how a query holds them, so that what a thousand living observations cost over a thousand made one at a time can be read as the difference
    /// </summary>
    void ConstructHoldAndDispose(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        for (var i = 0; i < elementCount; ++i)
            held[i] = observer.Observe(predicate, people[i]);
        for (var i = 0; i < elementCount; ++i)
            held[i].Dispose();
    }

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark]
    public void RankComparisonDirectHeld() =>
        ConstructHoldAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void RankComparisonGraphHeld() =>
        ConstructHoldAndDispose(graph, rankComparison);

    [GlobalSetup]
    public void Setup()
    {
        direct = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true });
        graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        held = new IObservableExpression<BenchmarkPersonWithPartner, bool>[elementCount];
        var collection = BenchmarkPersonWithPartner.CreateCollection(elementCount);
        people = new BenchmarkPersonWithPartner[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = collection[i];
    }
}
