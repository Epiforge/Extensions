namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ShapeEligibilityBenchmarks
{
    const int elementCount = 1000;

    static readonly BenchmarkPerson liveThreshold = new("liveThreshold", 0);

    static readonly Expression<Func<BenchmarkPerson, bool>> capturedTarget = person => person.Rank > liveThreshold.Rank;
    static readonly Expression<Func<BenchmarkPerson, double>> castSelector = person => (double)person.Rank;
    static readonly Expression<Func<BenchmarkPerson, int>> groupKeySelector = person => person.Rank % 16;
    static readonly Expression<Func<BenchmarkPerson, int>> orderKeySelector = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, int>> productSelector = person => person.Rank * 2;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankComparison = person => person.Rank > 0;

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Benchmark]
    public void CapturedTargetDirect() =>
        ConstructAndDispose(direct, capturedTarget);

    [Benchmark]
    public void CapturedTargetGraph() =>
        ConstructAndDispose(graph, capturedTarget);

    [Benchmark]
    public void CastSelectorDirect() =>
        ConstructAndDispose(direct, castSelector);

    [Benchmark]
    public void CastSelectorGraph() =>
        ConstructAndDispose(graph, castSelector);

    [Benchmark]
    public void GroupKeySelectorDirect() =>
        ConstructAndDispose(direct, groupKeySelector);

    [Benchmark]
    public void GroupKeySelectorGraph() =>
        ConstructAndDispose(graph, groupKeySelector);

    [Benchmark]
    public void OrderKeySelectorDirect() =>
        ConstructAndDispose(direct, orderKeySelector);

    [Benchmark]
    public void OrderKeySelectorGraph() =>
        ConstructAndDispose(graph, orderKeySelector);

    [Benchmark]
    public void ProductSelectorDirect() =>
        ConstructAndDispose(direct, productSelector);

    [Benchmark]
    public void ProductSelectorGraph() =>
        ConstructAndDispose(graph, productSelector);

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

    void ConstructAndDispose<TResult>(CollectionObserver observer, Expression<Func<BenchmarkPerson, TResult>> selector)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var select = sourceQuery.ObserveSelect(selector);
        select.Dispose();
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
