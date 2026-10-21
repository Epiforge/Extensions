namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices copying a query of a thousand elements into an array and into a list, as a caller does who materializes a query, against copying a list of the same elements
/// </summary>
/// <remarks>
/// Both copies go through the query's <see cref="ICollection{T}.CopyTo(T[], int)"/>. The source arms read through the query's indexer; the filter arms read through its enumerator, since a filter is costly to reach by index. The rank arms copy a value type, so that what a copy pays per element for a value type can be read against what it pays for a reference
/// </remarks>
[MemoryDiagnoser]
public class QueryMaterializationBenchmarks
{
    const int elementCount = 1000;

    List<BenchmarkPerson> list = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<int> ranks = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        where.Dispose();
        ranks.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public int[] RanksToArray() =>
        ranks.ToArray();

    [Benchmark]
    public List<int> RanksToList() =>
        new(ranks);

    [Benchmark(Baseline = true)]
    public BenchmarkPerson[] ReferenceListToArray() =>
        list.ToArray();

    [GlobalSetup]
    public void SetUp()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(elementCount);
        list = [.. source];
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(person => person.Rank >= 0);
        ranks = sourceQuery.ObserveSelect(person => person.Rank);
    }

    [Benchmark]
    public BenchmarkPerson[] SourceToArray() =>
        sourceQuery.ToArray();

    [Benchmark]
    public List<BenchmarkPerson> SourceToList() =>
        new(sourceQuery);

    [Benchmark]
    public BenchmarkPerson[] WhereToArray() =>
        where.ToArray();

    [Benchmark]
    public List<BenchmarkPerson> WhereToList() =>
        new(where);
}
