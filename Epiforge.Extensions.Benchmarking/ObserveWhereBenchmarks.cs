namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveWhereBenchmarks :
    CollectionQueryBenchmarks
{
    int indexerLimit;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [Benchmark]
    public int ChangeThenEnumerate()
    {
        Next().Rank ^= 2;
        var count = 0;
        foreach (var person in where)
            ++count;
        return count;
    }

    protected override void CleanupQuery() =>
        where.Dispose();

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var person in where)
            ++count;
        return count;
    }

    [Benchmark]
    public int EnumerateByIndexer()
    {
        var sum = 0;
        for (var i = 0; i < indexerLimit; ++i)
            sum += where[i].Rank;
        return sum;
    }

    [Benchmark]
    public int EnumerateThenSweepByIndexer()
    {
        var sum = 0;
        foreach (var person in where)
            sum += person.Rank;
        for (var i = 0; i < indexerLimit; ++i)
            sum += where[i].Rank;
        return sum;
    }

    protected override void SetupQuery()
    {
        where = SourceQuery.ObserveWhere(person => person.Rank % 2 == 0);
        indexerLimit = where.Count;
    }

    [Benchmark]
    public void SourceAddAndRemove()
    {
        Source.Add(Added);
        Source.RemoveAt(Source.Count - 1);
    }

    [Benchmark]
    public int SweepByIndexerOutOfOrder()
    {
        var sum = 0;
        for (var i = 0; i < indexerLimit; ++i)
            sum += where[(int)(Mixer.Mix((uint)i) % (uint)indexerLimit)].Rank;
        return sum;
    }

    [Benchmark]
    public void ValueChangeFlippingMembership() =>
        Next().Rank ^= 1;

    [Benchmark]
    public void ValueChangeKeepingMembership() =>
        Next().Rank ^= 2;
}
