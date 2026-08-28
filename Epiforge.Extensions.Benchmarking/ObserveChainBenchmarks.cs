namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveChainBenchmarks :
    CollectionQueryBenchmarks
{
    IObservableCollectionQuery<int> select = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    protected override void CleanupQuery()
    {
        select.Dispose();
        where.Dispose();
    }

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var projection in select)
            ++count;
        return count;
    }

    protected override void SetupQuery()
    {
        where = SourceQuery.ObserveWhere(person => person.Rank >= 0);
        select = where.ObserveSelect(person => person.Rank * 2);
    }

    [Benchmark]
    public void SourceAddAndRemove()
    {
        Source.Add(Added);
        Source.RemoveAt(Source.Count - 1);
    }

    [Benchmark]
    public void ValueChange() =>
        Next().Rank ^= 2;
}
