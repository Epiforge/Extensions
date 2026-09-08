namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Decomposes what one add and one remove cost a query which orders its source, by standing up in turn each layer that query holds inside itself: the collection alone, the observed source over it, the projection of every element to the pair of itself and its key, and the ordering over that projection
/// </summary>
/// <remarks>
/// The projection arm builds the pair by hand rather than through the ordered query, so that its selector is the shape <c>ObservableCollectionOrderByQuery</c> generates — a constructor of <see cref="Tuple{T1, T2}" /> over the element and a key converted to <see cref="IComparable" /> — and what the ordering costs beyond it can be told from what observing every element costs
/// </remarks>
[MemoryDiagnoser]
public class OrderByMembershipShareBenchmarks
{
    const int elementCount = 1000;

    BenchmarkPerson added = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    IObservableCollectionQuery<Tuple<BenchmarkPerson, IComparable>> select = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void AddAndRemove()
    {
        source.Add(added);
        source.RemoveAt(source.Count - 1);
    }

    /// <summary>
    /// What the change costs the collection before anything observes it
    /// </summary>
    [Benchmark(Baseline = true)]
    public void AddAndRemoveUnobserved() =>
        AddAndRemove();

    /// <summary>
    /// The same change with the whole ordered query standing
    /// </summary>
    [Benchmark]
    public void AddAndRemoveWithOrderedQuery() =>
        AddAndRemove();

    /// <summary>
    /// The same change with every element projected to the pair of itself and its key, which is what an ordered query orders over
    /// </summary>
    [Benchmark]
    public void AddAndRemoveWithSelectQuery() =>
        AddAndRemove();

    /// <summary>
    /// The same change with the source observed and nothing built over it
    /// </summary>
    [Benchmark]
    public void AddAndRemoveWithSourceQuery() =>
        AddAndRemove();

    [GlobalCleanup(Target = nameof(AddAndRemoveWithOrderedQuery))]
    public void CleanupOrderedQuery()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(AddAndRemoveWithSelectQuery))]
    public void CleanupSelectQuery()
    {
        select.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(AddAndRemoveWithSourceQuery))]
    public void CleanupSourceQuery() =>
        sourceQuery.Dispose();

    [GlobalSetup(Target = nameof(AddAndRemoveWithOrderedQuery))]
    public void SetupOrderedQuery()
    {
        SetupSourceQuery();
        orderBy = sourceQuery.ObserveOrderBy(person => person.Rank);
    }

    [GlobalSetup(Target = nameof(AddAndRemoveWithSelectQuery))]
    public void SetupSelectQuery()
    {
        SetupSourceQuery();
        select = sourceQuery.ObserveSelect(person => new Tuple<BenchmarkPerson, IComparable>(person, person.Rank));
    }

    void SetupSource()
    {
        observer = new CollectionObserver();
        source = BenchmarkPerson.CreateCollection(elementCount);
        added = new BenchmarkPerson("Added", 0);
    }

    [GlobalSetup(Target = nameof(AddAndRemoveWithSourceQuery))]
    public void SetupSourceQuery()
    {
        SetupSource();
        sourceQuery = observer.ObserveReadOnlyList(source);
    }

    [GlobalSetup(Target = nameof(AddAndRemoveUnobserved))]
    public void SetupUnobserved() =>
        SetupSource();
}
