namespace Epiforge.Extensions.Benchmarking;

public abstract class CollectionQueryBenchmarks
{
    uint counter;
    int cursor;

    protected BenchmarkPerson Added = null!;
    protected CollectionObserver Observer = null!;
    protected ObservableRangeCollection<BenchmarkPerson> Source = null!;
    protected IObservableCollectionQuery<BenchmarkPerson> SourceQuery = null!;

    [Params(100, 1000, 10000)]
    public int ElementCount { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        CleanupQuery();
        SourceQuery.Dispose();
    }

    protected abstract void CleanupQuery();

    protected BenchmarkPerson Next()
    {
        var person = Source[cursor];
        cursor = cursor + 1 == Source.Count ? 0 : cursor + 1;
        return person;
    }

    protected int NextKey() =>
        (int)(Mixer.Mix(++counter) & 0x00ffffff);

    [GlobalSetup]
    public void Setup()
    {
        Observer = new CollectionObserver();
        Source = BenchmarkPerson.CreateCollection(ElementCount);
        SourceQuery = Observer.ObserveReadOnlyList(Source);
        Added = new BenchmarkPerson("Added", 0);
        SetupQuery();
    }

    protected abstract void SetupQuery();
}
