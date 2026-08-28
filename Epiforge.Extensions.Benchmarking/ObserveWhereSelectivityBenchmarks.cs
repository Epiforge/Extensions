namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveWhereSelectivityBenchmarks
{
    const int elementCount = 10000;

    int indexerLimit;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [Params(2, 8, 16, 32, 128)]
    public int OneIn { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

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
    public int EnumerateByIndexerBackward()
    {
        var sum = 0;
        for (var i = indexerLimit - 1; i >= 0; --i)
            sum += where[i].Rank;
        return sum;
    }

    [Benchmark]
    public int EnumerateByIndexerInterleaved()
    {
        var sum = 0;
        var half = (indexerLimit + 1) / 2;
        for (var i = 0; i < half; ++i)
        {
            sum += where[i].Rank;
            var paired = i + half;
            if (paired < indexerLimit)
                sum += where[paired].Rank;
        }
        return sum;
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(elementCount);
        sourceQuery = observer.ObserveReadOnlyList(source);
        var oneIn = OneIn;
        where = sourceQuery.ObserveWhere(person => person.Rank % oneIn == 0);
        indexerLimit = where.Count;
    }

    [Benchmark]
    public int SweepByIndexerOutOfOrder()
    {
        var sum = 0;
        for (var i = 0; i < indexerLimit; ++i)
            sum += where[(int)(Mixer.Mix((uint)i) % (uint)indexerLimit)].Rank;
        return sum;
    }
}
