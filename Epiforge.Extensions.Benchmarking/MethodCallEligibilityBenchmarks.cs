namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
public class MethodCallEligibilityBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> methodCall = person => string.IsNullOrEmpty(person.Name);
    static readonly Expression<Func<BenchmarkPerson, bool>> rankComparison = person => person.Rank > 0;

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Benchmark]
    public void MethodCallDirect() =>
        ConstructAndDispose(direct, methodCall);

    [Benchmark]
    public void MethodCallGraph() =>
        ConstructAndDispose(graph, methodCall);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

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
