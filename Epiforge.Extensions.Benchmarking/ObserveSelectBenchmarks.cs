namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveSelectBenchmarks :
    CollectionQueryBenchmarks
{
    int indexerLimit;
    IObservableCollectionQuery<int> select = null!;

    [Benchmark]
    public int ChangeThenEnumerate()
    {
        Next().Rank ^= 2;
        var count = 0;
        foreach (var projection in select)
            ++count;
        return count;
    }

    protected override void CleanupQuery() =>
        select.Dispose();

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var projection in select)
            ++count;
        return count;
    }

    [Benchmark]
    public int EnumerateByIndexer()
    {
        var sum = 0;
        for (var i = 0; i < indexerLimit; ++i)
            sum += select[i];
        return sum;
    }

    [Benchmark]
    public int EnumerateByIndexerBackward()
    {
        var sum = 0;
        for (var i = indexerLimit - 1; i >= 0; --i)
            sum += select[i];
        return sum;
    }

    [Benchmark]
    public int EnumerateByIndexerInterleaved()
    {
        var sum = 0;
        var half = (indexerLimit + 1) / 2;
        for (var i = 0; i < half; ++i)
        {
            sum += select[i];
            var paired = i + half;
            if (paired < indexerLimit)
                sum += select[paired];
        }
        return sum;
    }

    [Benchmark]
    public int EnumerateThenSweepByIndexer()
    {
        var sum = 0;
        foreach (var projection in select)
            sum += projection;
        for (var i = 0; i < indexerLimit; ++i)
            sum += select[i];
        return sum;
    }

    protected override void SetupQuery()
    {
        select = SourceQuery.ObserveSelect(person => person.Rank * 2);
        indexerLimit = select.Count;
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
            sum += select[(int)(Mixer.Mix((uint)i) % (uint)indexerLimit)];
        return sum;
    }

    [Benchmark]
    public void ValueChange() =>
        Next().Rank ^= 2;
}
