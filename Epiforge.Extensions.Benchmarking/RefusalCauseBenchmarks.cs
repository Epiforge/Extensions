namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class RefusalCauseBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> deferredBranchOverAPropertyChain = person => person.Rank % 2 == 0 && person.Name.Length > 1;
    static readonly Expression<Func<BenchmarkPerson, bool>> propertyChain = person => person.Name.Length > 1;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankComparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> shortCircuitOverOneProperty = person => person.Rank > 0 && person.Rank < 100;
    static readonly Expression<Func<BenchmarkPerson, bool>> shortCircuitOverTwoMembers = person => person.Rank > 0 && person.Name != null;

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Benchmark]
    public void DeferredBranchOverAPropertyChainDirect() =>
        ConstructAndDispose(direct, deferredBranchOverAPropertyChain);

    [Benchmark]
    public void DeferredBranchOverAPropertyChainGraph() =>
        ConstructAndDispose(graph, deferredBranchOverAPropertyChain);

    [Benchmark]
    public void PropertyChainDirect() =>
        ConstructAndDispose(direct, propertyChain);

    [Benchmark]
    public void PropertyChainGraph() =>
        ConstructAndDispose(graph, propertyChain);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void ShortCircuitOverOnePropertyDirect() =>
        ConstructAndDispose(direct, shortCircuitOverOneProperty);

    [Benchmark]
    public void ShortCircuitOverOnePropertyGraph() =>
        ConstructAndDispose(graph, shortCircuitOverOneProperty);

    [Benchmark]
    public void ShortCircuitOverTwoMembersDirect() =>
        ConstructAndDispose(direct, shortCircuitOverTwoMembers);

    [Benchmark]
    public void ShortCircuitOverTwoMembersGraph() =>
        ConstructAndDispose(graph, shortCircuitOverTwoMembers);

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
