namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveDictionaryWhereBenchmarks
{
    CollectionObserver observer = null!;
    ObservableDictionary<int, BenchmarkPerson> source = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> sourceQuery = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> where = null!;

    [Params(100, 1000, 10000)]
    public int ElementCount { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Count() =>
        where.Count;

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var pair in where)
            ++count;
        return count;
    }

    [Benchmark]
    public object ReadKeys() =>
        where.Keys;

    [Benchmark]
    public object ReadValues() =>
        where.Values;

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        source = new ObservableDictionary<int, BenchmarkPerson>();
        for (var i = 0; i < ElementCount; ++i)
            source.Add(i, new BenchmarkPerson($"P{i}", i));
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        where = sourceQuery.ObserveWhere((key, person) => person.Rank >= 0);
    }
}
