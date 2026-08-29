namespace Epiforge.Extensions.Benchmarking;

using System.Linq.Expressions;

[MemoryDiagnoser]
public class QueryFootprintBenchmarks
{
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    BenchmarkPerson threshold = null!;

    [Params(250, 1000, 4000, 10000)]
    public int ElementCount { get; set; }

    [Benchmark]
    public void FiveNodes() =>
        ConstructAndDispose(person => person.Rank % 2 == 0);

    [Benchmark]
    public void ElevenNodes() =>
        ConstructAndDispose(person => person.Rank % 2 == 0 && person.Name.Length > 1);

    [Benchmark(Baseline = true)]
    public void OneNode() =>
        ConstructAndDispose(person => true);

    [Benchmark]
    public void ThreeNodes() =>
        ConstructAndDispose(person => person.Rank > 0);

    [Benchmark]
    public void SharedChangeableSubexpression() =>
        ConstructAndDispose(person => person.Rank > threshold.Rank);

    void ConstructAndDispose(Expression<Func<BenchmarkPerson, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(ElementCount);
        threshold = new BenchmarkPerson("threshold", 0);
    }
}
