namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what a concatenation's consumer pays for reading it by index where the concatenation does not report that its sources charge for indexed reads, against reading it by enumeration where it does
/// </summary>
/// <remarks>
/// The comparison query and the filter both choose between the indexer and an enumerator by the flag their source reports. A concatenation of two projections or two filters reported no penalty, so its consumers read it by index while paying its sources' indexed cost on every read. The concatenation and everything under it stand for the life of the class, so each arm constructs and disposes only the consumer, which is the part the flag decides. The arms over a single projection and a single filter already take the enumerator and are the controls. The arm over a concatenation of two lists was built as a third control and became the deciding arm instead: its sources carry no penalty, so it measures what the concatenation's own indexer costs, which takes a lock on every read, against enumerating it
/// </remarks>
[MemoryDiagnoser]
public class ConcatIndexerPenaltyBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsNotNegative = person => person.Rank >= 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsPositive = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPerson, int>> rankRead = person => person.Rank;

    CollectionObserver collectionObserver = null!;
    IObservableCollectionQuery<int> concatOfLists = null!;
    IObservableCollectionQuery<int> concatOfSelects = null!;
    IObservableCollectionQuery<BenchmarkPerson> concatOfWheres = null!;
    IObservableCollectionQuery<int> select = null!;
    readonly List<IDisposable> standing = [];
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark]
    public void MaxOverConcatOfLists()
    {
        var max = concatOfLists.ObserveMax();
        max.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void MaxOverConcatOfSelects()
    {
        var max = concatOfSelects.ObserveMax();
        max.Dispose();
    }

    [Benchmark]
    public void MaxOverSelect()
    {
        var max = select.ObserveMax();
        max.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        var firstPeople = BenchmarkPerson.CreateCollection(elementCount);
        var secondPeople = BenchmarkPerson.CreateCollection(elementCount);
        var firstRanks = new ObservableRangeCollection<int>(Enumerable.Range(0, elementCount));
        var secondRanks = new ObservableRangeCollection<int>(Enumerable.Range(0, elementCount));
        var firstPeopleQuery = Stand(collectionObserver.ObserveReadOnlyList(firstPeople));
        var secondPeopleQuery = Stand(collectionObserver.ObserveReadOnlyList(secondPeople));
        var firstRanksQuery = Stand(collectionObserver.ObserveReadOnlyList(firstRanks));
        var secondRanksQuery = Stand(collectionObserver.ObserveReadOnlyList(secondRanks));
        concatOfLists = Stand(firstRanksQuery.ObserveConcat(secondRanksQuery));
        var firstSelect = Stand(firstPeopleQuery.ObserveSelect(rankRead));
        var secondSelect = Stand(secondPeopleQuery.ObserveSelect(rankRead));
        concatOfSelects = Stand(firstSelect.ObserveConcat(secondSelect));
        var firstWhere = Stand(firstPeopleQuery.ObserveWhere(rankIsNotNegative));
        var secondWhere = Stand(secondPeopleQuery.ObserveWhere(rankIsNotNegative));
        concatOfWheres = Stand(firstWhere.ObserveConcat(secondWhere));
        select = firstSelect;
        where = firstWhere;
    }

    T Stand<T>(T query)
        where T : IDisposable
    {
        standing.Add(query);
        return query;
    }

    [Benchmark]
    public void WhereOverConcatOfWheres()
    {
        var filtered = concatOfWheres.ObserveWhere(rankIsPositive);
        filtered.Dispose();
    }

    [Benchmark]
    public void WhereOverWhere()
    {
        var filtered = where.ObserveWhere(rankIsPositive);
        filtered.Dispose();
    }
}
