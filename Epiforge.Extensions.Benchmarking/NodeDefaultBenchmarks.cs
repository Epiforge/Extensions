namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class NodeDefaultBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> comparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, string>> selector = person => person.Name;

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    void ConstructAndDisposeSelect(CollectionObserver observer)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var select = sourceQuery.ObserveSelect(selector);
        select.Dispose();
        sourceQuery.Dispose();
    }

    void ConstructAndDisposeWhere(CollectionObserver observer)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(comparison);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void ConstructComparisonDirect() =>
        ConstructAndDisposeWhere(direct);

    [Benchmark(Baseline = true)]
    public void ConstructComparisonGraph() =>
        ConstructAndDisposeWhere(graph);

    [Benchmark]
    public void ConstructStringSelectorGraph() =>
        ConstructAndDisposeSelect(graph);

    [GlobalSetup]
    public void Setup()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        source = BenchmarkPerson.CreateCollection(elementCount);
    }
}
