namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class QueryFanOutBenchmarks
{
    const int elementCount = 1000;

    IObservableCollectionQuery<BenchmarkPerson> live = null!;
    CollectionObserver liveObserver = null!;
    ObservableRangeCollection<BenchmarkPerson> liveSource = null!;
    IObservableCollectionQuery<BenchmarkPerson> liveSourceQuery = null!;
    BenchmarkPerson liveThreshold = null!;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    BenchmarkPerson threshold = null!;

    [Benchmark]
    public void ChangeTheSharedValue() =>
        liveThreshold.Rank ^= 1;

    [GlobalCleanup]
    public void Cleanup()
    {
        live.Dispose();
        liveSourceQuery.Dispose();
    }

    [Benchmark]
    public void ConstructAndDisposeWithFanOut()
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank > threshold.Rank);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void ConstructAndDisposeWithoutFanOut()
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(person => person.Rank > 0);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(elementCount);
        threshold = new BenchmarkPerson("threshold", 0);
        liveObserver = new CollectionObserver();
        liveSource = BenchmarkPerson.CreateCollection(elementCount);
        liveThreshold = new BenchmarkPerson("liveThreshold", 0);
        liveSourceQuery = liveObserver.ObserveReadOnlyList(liveSource);
        live = liveSourceQuery.ObserveWhere(person => person.Rank > liveThreshold.Rank);
    }
}
