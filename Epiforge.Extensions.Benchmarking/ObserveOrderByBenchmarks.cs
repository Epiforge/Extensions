namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveOrderByBenchmarks :
    CollectionQueryBenchmarks
{
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;

    protected override void CleanupQuery() =>
        orderBy.Dispose();

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var person in orderBy)
            ++count;
        return count;
    }

    [Benchmark]
    public void KeyChange() =>
        Next().Rank = NextKey();

    protected override void SetupQuery() =>
        orderBy = SourceQuery.ObserveOrderBy(person => person.Rank);

    [Benchmark]
    public void SourceAddAndRemove()
    {
        Source.Add(Added);
        Source.RemoveAt(Source.Count - 1);
    }
}
