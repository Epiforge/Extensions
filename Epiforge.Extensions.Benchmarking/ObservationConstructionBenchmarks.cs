namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObservationConstructionBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> constantPredicate = person => true;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    ExpressionObserver direct = null!;
    ExpressionObserver graph = null!;
    BenchmarkPersonWithPartner[] people = null!;

    [Benchmark]
    public void ConstantDirect() =>
        ConstructAndDispose(direct, constantPredicate);

    [Benchmark]
    public void ConstantGraph() =>
        ConstructAndDispose(graph, constantPredicate);

    void ConstructAndDispose(ExpressionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        for (var i = 0; i < elementCount; ++i)
            observer.Observe(predicate, people[i]).Dispose();
    }

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [GlobalSetup]
    public void Setup()
    {
        direct = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true });
        graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var collection = BenchmarkPersonWithPartner.CreateCollection(elementCount);
        people = new BenchmarkPersonWithPartner[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = collection[i];
    }
}
