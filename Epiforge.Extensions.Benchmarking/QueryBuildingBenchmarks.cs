namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices building and disposing a filtered view over sources which read through different numbers of queries, so that what a build pays for each query whose lock it holds can be read from the differences between the arms
/// </summary>
/// <remarks>
/// Every source is empty, so that no expression is observed and nothing but the building of the query is measured. A filtered view built over an observation or over another filtered view holds one lock while it reads and subscribes; one built over a concatenation holds the concatenation's and those of everything it reads through, so over a concatenation of two observations three, of four seven and of eight fifteen. Each arm builds over each of 256 standing sources, none of which shares a query with another, and then disposes of what it built. The enumeration arms read each standing concatenation once, which a build over it also does, so that the reading can be subtracted from the building
/// </remarks>
[AgainstReleasedExpressions("7.0.0")]
[MemoryDiagnoser]
public class QueryBuildingBenchmarks
{
    const int SourceCount = 256;

    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsNonNegative = person => person.Rank >= 0;

    readonly IDisposable[] built = new IDisposable[SourceCount];
    IObservableCollectionQuery<BenchmarkPerson>[] concatenationsOfEight = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] concatenationsOfFour = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] concatenationsOfTwo = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] observations = null!;
    readonly List<IDisposable> standing = [];
    IObservableCollectionQuery<BenchmarkPerson>[] wheres = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    IObservableCollectionQuery<BenchmarkPerson> Concatenate(CollectionObserver collectionObserver, int count)
    {
        var concatenation = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>()));
        for (var i = 1; i < count; ++i)
            concatenation = Stand(concatenation.ObserveConcat(Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>()))));
        return concatenation;
    }

    static int Enumerate(IObservableCollectionQuery<BenchmarkPerson>[] sources)
    {
        var count = 0;
        for (var i = 0; i < SourceCount; ++i)
            foreach (var person in sources[i])
                ++count;
        return count;
    }

    [Benchmark]
    public int EnumerateConcatenationOfEight() =>
        Enumerate(concatenationsOfEight);

    [Benchmark]
    public int EnumerateConcatenationOfTwo() =>
        Enumerate(concatenationsOfTwo);

    void Release()
    {
        for (var i = SourceCount - 1; i >= 0; --i)
            built[i].Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        var collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit }));
        observations = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        wheres = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        concatenationsOfTwo = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        concatenationsOfFour = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        concatenationsOfEight = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        for (var i = 0; i < SourceCount; ++i)
        {
            observations[i] = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>()));
            wheres[i] = Stand(Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>())).ObserveWhere(rankIsNonNegative));
            concatenationsOfTwo[i] = Concatenate(collectionObserver, 2);
            concatenationsOfFour[i] = Concatenate(collectionObserver, 4);
            concatenationsOfEight[i] = Concatenate(collectionObserver, 8);
        }
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }

    void Where(IObservableCollectionQuery<BenchmarkPerson>[] sources)
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = sources[i].ObserveWhere(rankIsNonNegative);
        Release();
    }

    [Benchmark]
    public void WhereOverConcatenationOfEight() =>
        Where(concatenationsOfEight);

    [Benchmark]
    public void WhereOverConcatenationOfFour() =>
        Where(concatenationsOfFour);

    [Benchmark]
    public void WhereOverConcatenationOfTwo() =>
        Where(concatenationsOfTwo);

    [Benchmark(Baseline = true)]
    public void WhereOverObservation() =>
        Where(observations);

    [Benchmark]
    public void WhereOverWhere() =>
        Where(wheres);
}
