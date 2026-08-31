namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class OperatorEligibilityBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> deferredBranch = person => person.Rank % 2 == 0 && person.Name.Length > 1;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankComparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> stringComparison = person => person.Name == "Emily";

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Benchmark]
    public void DeferredBranchDirect() =>
        ConstructAndDispose(direct, deferredBranch);

    [Benchmark]
    public void DeferredBranchGraph() =>
        ConstructAndDispose(graph, deferredBranch);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void StringComparisonDirect() =>
        ConstructAndDispose(direct, stringComparison);

    [Benchmark]
    public void StringComparisonGraph() =>
        ConstructAndDispose(graph, stringComparison);

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
