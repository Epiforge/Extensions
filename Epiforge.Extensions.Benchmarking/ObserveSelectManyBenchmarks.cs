namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveSelectManyBenchmarks
{
    BenchmarkPerson added = null!;
    int cursor;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> selectMany = null!;
    ObservableRangeCollection<BenchmarkTeam> source = null!;
    IObservableCollectionQuery<BenchmarkTeam> sourceQuery = null!;

    [Params(100, 1000, 10000)]
    public int TeamCount { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        selectMany.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var person in selectMany)
            ++count;
        return count;
    }

    [Benchmark]
    public void InnerAddAndRemove()
    {
        var people = source[cursor].People;
        cursor = cursor + 1 == source.Count ? 0 : cursor + 1;
        people.Add(added);
        people.RemoveAt(people.Count - 1);
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        var teams = new List<BenchmarkTeam>(TeamCount);
        for (var i = 0; i < TeamCount; ++i)
            teams.Add(new BenchmarkTeam(BenchmarkPerson.CreateCollection(4)));
        source = new ObservableRangeCollection<BenchmarkTeam>(teams);
        sourceQuery = observer.ObserveReadOnlyList(source);
        selectMany = sourceQuery.ObserveSelectMany(team => team.People);
        added = new BenchmarkPerson("Added", 0);
    }
}
