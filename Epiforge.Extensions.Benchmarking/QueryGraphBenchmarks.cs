namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class QueryGraphBenchmarks
{
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;

    [Params(100, 1000, 10000)]
    public int ElementCount { get; set; }

    [Benchmark]
    public void ConstructAndDisposeAWhere()
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank % 2 == 0);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(ElementCount);
    }
}
