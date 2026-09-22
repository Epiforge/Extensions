namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what GS5 does today to select from a collection by rank and to rank an element within one, so that the operators it asked for can be measured against the shapes they replace
/// </summary>
/// <remarks>
/// A maximum of text, which GS5 currently takes as the first of an ordering so that it can compare under a culture of its choosing; an element at a fixed position of an ordering, which stands in for a median whose position will follow the count; and the number of elements ahead of one, which is the only way to rank an element before a rank operator exists, both constructed and propagated through one change. The source and the ordering by rank stand for the life of the class, so each construction arm builds and disposes only what it names. Each propagation arm changes a collection of its own, so that no arm pays for what another arm keeps standing, and the change with nothing observing it is the floor to subtract: two event arguments raised and nobody listening. The arms for the operators themselves were added once they existed; the rank's propagation arm keeps an ordering standing, because a rank cannot be had without one, and the ordering alone is priced beside it so that the rank's own share can be read off
/// </remarks>
[MemoryDiagnoser]
public class QuerySelectionBenchmarks
{
    const int elementCount = 1000;

    static readonly Func<int, int> middleOfCount = count => count / 2;
    static readonly Expression<Func<BenchmarkPerson, IComparable>> nameKey = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, string>> nameRead = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, IComparable>> rankKey = person => person.Rank;

    CollectionObserver collectionObserver = null!;
    ObservableRangeCollection<BenchmarkPerson> countedPeople = null!;
    BenchmarkPerson countedSubject = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderedByRank = null!;
    ObservableRangeCollection<BenchmarkPerson> orderedPeople = null!;
    ObservableRangeCollection<BenchmarkPerson> people = null!;
    ObservableRangeCollection<BenchmarkPerson> rankedPeople = null!;
    IObservableCollectionQuery<BenchmarkPerson> source = null!;
    readonly List<IDisposable> standing = [];
    BenchmarkPerson subject = null!;
    ObservableRangeCollection<BenchmarkPerson> unobservedPeople = null!;

    [Benchmark]
    public void ChangeObservedByNothing() =>
        Toggle(unobservedPeople);

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark]
    public void CountAheadConstructed()
    {
        var count = source.ObserveCount(person => person.Rank > subject.Rank);
        count.Dispose();
    }

    [Benchmark]
    public void CountAheadPropagated() =>
        Toggle(countedPeople);

    [Benchmark]
    public void ElementAtCountedPositionOfOrdering()
    {
        var middle = orderedByRank.ObserveElementAt(middleOfCount);
        middle.Dispose();
    }

    [Benchmark]
    public void ElementAtFixedPositionOfOrdering()
    {
        var middle = orderedByRank.ObserveElementAt(elementCount / 2);
        middle.Dispose();
    }

    [Benchmark]
    public void FirstOfOrderingByName()
    {
        var ordered = source.ObserveOrderBy(nameKey, true);
        var first = ordered.ObserveFirst();
        first.Dispose();
        ordered.Dispose();
    }

    [Benchmark]
    public void MaxByName()
    {
        var max = source.ObserveMaxBy(nameRead, StringComparer.Ordinal);
        max.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void MaxOfNames()
    {
        var max = source.ObserveMax(nameRead);
        max.Dispose();
    }

    [Benchmark]
    public void MaxOfNamesByComparer()
    {
        var max = source.ObserveMax(nameRead, StringComparer.Ordinal);
        max.Dispose();
    }

    [Benchmark]
    public void OrderingPropagated() =>
        Toggle(orderedPeople);

    [Benchmark]
    public void RankConstructed()
    {
        var rank = source.ObserveRank(subject, rankKey, false);
        rank.Dispose();
    }

    [Benchmark]
    public void RankPropagated() =>
        Toggle(rankedPeople);

    [GlobalSetup]
    public void Setup()
    {
        collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        people = BenchmarkPerson.CreateCollection(elementCount);
        subject = people[elementCount / 2];
        source = Stand(collectionObserver.ObserveReadOnlyList(people));
        orderedByRank = Stand(source.ObserveOrderBy(rankKey));
        countedPeople = BenchmarkPerson.CreateCollection(elementCount);
        countedSubject = countedPeople[elementCount / 2];
        var countedSource = Stand(collectionObserver.ObserveReadOnlyList(countedPeople));
        Stand(countedSource.ObserveCount(person => person.Rank > countedSubject.Rank));
        unobservedPeople = BenchmarkPerson.CreateCollection(elementCount);
        orderedPeople = BenchmarkPerson.CreateCollection(elementCount);
        Stand(Stand(collectionObserver.ObserveReadOnlyList(orderedPeople)).ObserveOrderBy(rankKey));
        rankedPeople = BenchmarkPerson.CreateCollection(elementCount);
        var rankedSource = Stand(collectionObserver.ObserveReadOnlyList(rankedPeople));
        Stand(rankedSource.ObserveOrderBy(rankKey));
        Stand(rankedSource.ObserveRank(rankedPeople[elementCount / 2], rankKey, false));
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }

    static void Toggle(ObservableRangeCollection<BenchmarkPerson> collection)
    {
        var changed = collection[100];
        changed.Rank = elementCount + 1;
        changed.Rank = 100;
    }
}
