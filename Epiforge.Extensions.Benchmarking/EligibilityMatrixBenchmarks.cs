namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class EligibilityMatrixBenchmarks
{
    const int elementCount = 1000;

    static readonly BenchmarkPerson staticThreshold = new("staticThreshold", 0);

    static readonly Expression<Func<BenchmarkPerson, bool>> argumentIndependent = person => true;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankComparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> shortCircuit = person => person.Rank > 0 && person.Rank < 100;
    static readonly Expression<Func<BenchmarkPerson, bool>> staticFieldTarget = person => person.Rank > staticThreshold.Rank;
    static readonly Expression<Func<BenchmarkPerson, bool>> staticPropertyValue = person => person.Rank > Environment.ProcessorCount;

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Benchmark]
    public void ArgumentIndependentDirect() =>
        ConstructAndDispose(direct, argumentIndependent);

    [Benchmark]
    public void ArgumentIndependentGraph() =>
        ConstructAndDispose(graph, argumentIndependent);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void ShortCircuitDirect() =>
        ConstructAndDispose(direct, shortCircuit);

    [Benchmark]
    public void ShortCircuitGraph() =>
        ConstructAndDispose(graph, shortCircuit);

    [Benchmark]
    public void StaticFieldTargetDirect() =>
        ConstructAndDispose(direct, staticFieldTarget);

    [Benchmark]
    public void StaticFieldTargetGraph() =>
        ConstructAndDispose(graph, staticFieldTarget);

    [Benchmark]
    public void StaticPropertyValueDirect() =>
        ConstructAndDispose(direct, staticPropertyValue);

    [Benchmark]
    public void StaticPropertyValueGraph() =>
        ConstructAndDispose(graph, staticPropertyValue);

    void ConstructAndDispose(CollectionObserver observer, Expression<Func<BenchmarkPerson, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        source = BenchmarkPerson.CreateCollection(elementCount);
    }
}
