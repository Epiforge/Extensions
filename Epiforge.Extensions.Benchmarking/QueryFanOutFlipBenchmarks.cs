namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class QueryFanOutFlipBenchmarks
{
    const int elementCount = 1000;

    IObservableCollectionQuery<BenchmarkPerson> many = null!;
    CollectionObserver manyObserver = null!;
    ObservableRangeCollection<BenchmarkPerson> manySource = null!;
    IObservableCollectionQuery<BenchmarkPerson> manySourceQuery = null!;
    BenchmarkPerson manyThreshold = null!;
    IObservableCollectionQuery<BenchmarkPerson> one = null!;
    CollectionObserver oneObserver = null!;
    ObservableRangeCollection<BenchmarkPerson> oneSource = null!;
    IObservableCollectionQuery<BenchmarkPerson> oneSourceQuery = null!;
    BenchmarkPerson oneThreshold = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        many.Dispose();
        manySourceQuery.Dispose();
        one.Dispose();
        oneSourceQuery.Dispose();
    }

    [Benchmark]
    public void FlipHalfTheElements() =>
        manyThreshold.Rank = manyThreshold.Rank == 0 ? elementCount / 2 : 0;

    [Benchmark(Baseline = true)]
    public void FlipOneElement() =>
        oneThreshold.Rank ^= 1;

    [GlobalSetup]
    public void Setup()
    {
        manyObserver = new CollectionObserver();
        manySource = BenchmarkPerson.CreateCollection(elementCount);
        manyThreshold = new BenchmarkPerson("manyThreshold", 0);
        manySourceQuery = manyObserver.ObserveReadOnlyList(manySource);
        many = manySourceQuery.ObserveWhere(person => person.Rank > manyThreshold.Rank);
        oneObserver = new CollectionObserver();
        oneSource = BenchmarkPerson.CreateCollection(elementCount);
        oneThreshold = new BenchmarkPerson("oneThreshold", 0);
        oneSourceQuery = oneObserver.ObserveReadOnlyList(oneSource);
        one = oneSourceQuery.ObserveWhere(person => person.Rank > oneThreshold.Rank);
    }
}
