namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class PropagationFloorBenchmarks
{
    const int elementCount = 1000;

    BenchmarkPerson noneThreshold = null!;
    IObservableCollectionQuery<BenchmarkPerson> noneWhere = null!;
    CollectionObserver observer = null!;
    BenchmarkPerson oneThreshold = null!;
    IObservableCollectionQuery<BenchmarkPerson> oneWhere = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    [Benchmark(Baseline = true)]
    public void ChangeAffectingNoElement() =>
        noneThreshold.Rank = noneThreshold.Rank == -1 ? -2 : -1;

    [Benchmark]
    public void ChangeAffectingOneElement() =>
        oneThreshold.Rank = oneThreshold.Rank == 0 ? 1 : 0;

    [GlobalCleanup]
    public void Cleanup()
    {
        noneWhere.Dispose();
        oneWhere.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        source = BenchmarkPerson.CreateCollection(elementCount);
        sourceQuery = observer.ObserveReadOnlyList(source);
        noneThreshold = new BenchmarkPerson("noneThreshold", -1);
        oneThreshold = new BenchmarkPerson("oneThreshold", 0);
        noneWhere = sourceQuery.ObserveWhere(person => person.Rank > noneThreshold.Rank);
        oneWhere = sourceQuery.ObserveWhere(person => person.Rank > oneThreshold.Rank);
    }
}
