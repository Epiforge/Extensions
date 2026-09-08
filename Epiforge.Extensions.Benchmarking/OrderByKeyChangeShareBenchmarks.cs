namespace Epiforge.Extensions.Benchmarking;

using System.Collections.Specialized;

/// <summary>
/// Decomposes the 776 bytes one key change costs an ordered query, by varying whether the change reorders anything, whether anything is subscribed to the result, and whether the key is a value type which the public signature must box
/// </summary>
[MemoryDiagnoser]
public class OrderByKeyChangeShareBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, IComparable>> nameKey = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, IComparable>> rankKey = person => person.Rank;

    int cursor;
    string[] farNames = null!;
    bool[] flipped = null!;
    string[] nearNames = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    int observedChanges;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void Advance() =>
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;

    void ChangeRank(bool far)
    {
        var person = source[cursor];
        var home = cursor * 10;
        person.Rank = flipped[cursor] ? home : far ? (elementCount - 1 - cursor) * 10 + 5 : home + 1;
        flipped[cursor] = !flipped[cursor];
        Advance();
    }

    /// <summary>
    /// Moves one element the length of the collection, which is the change the scaling arms make
    /// </summary>
    [Benchmark(Baseline = true)]
    public void FarKeyChange() =>
        ChangeRank(true);

    /// <summary>
    /// The same move over a key which is already a reference, so that the box the <see cref="IComparable" /> signature forces on a value type is absent
    /// </summary>
    [Benchmark]
    public void FarKeyChangeOnAStringKey()
    {
        var person = source[cursor];
        person.Name = flipped[cursor] ? nearNames[cursor] : farNames[cursor];
        flipped[cursor] = !flipped[cursor];
        Advance();
    }

    /// <summary>
    /// The same move with something subscribed to the ordered result, which is what makes the query raise its own notification
    /// </summary>
    [Benchmark]
    public void FarKeyChangeWithSubscriber() =>
        ChangeRank(true);

    /// <summary>
    /// A change to the key which cannot reorder anything, the keys being ten apart and the change one, so that what the key change costs can be told from what the reordering costs
    /// </summary>
    [Benchmark]
    public void NearKeyChange() =>
        ChangeRank(false);

    void OrderByCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ++observedChanges;

    void SetupQuery(Expression<Func<BenchmarkPerson, IComparable>> keySelector)
    {
        observer = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        nearNames = new string[elementCount];
        farNames = new string[elementCount];
        flipped = new bool[elementCount];
        var people = new List<BenchmarkPerson>(elementCount);
        for (var i = 0; i < elementCount; ++i)
        {
            nearNames[i] = $"P{i:D4}";
            farNames[i] = $"P{elementCount - 1 - i:D4}A";
            people.Add(new BenchmarkPerson(nearNames[i], i * 10));
        }
        source = new ObservableRangeCollection<BenchmarkPerson>(people);
        sourceQuery = observer.ObserveReadOnlyList(source);
        orderBy = sourceQuery.ObserveOrderBy(keySelector);
    }

    [GlobalSetup(Targets = [nameof(FarKeyChange), nameof(NearKeyChange)])]
    public void SetupRankKey() =>
        SetupQuery(rankKey);

    [GlobalSetup(Target = nameof(FarKeyChangeOnAStringKey))]
    public void SetupNameKey() =>
        SetupQuery(nameKey);

    [GlobalSetup(Target = nameof(FarKeyChangeWithSubscriber))]
    public void SetupSubscribed()
    {
        SetupQuery(rankKey);
        orderBy.CollectionChanged += OrderByCollectionChanged;
    }

    [GlobalCleanup(Targets = [nameof(FarKeyChange), nameof(FarKeyChangeOnAStringKey), nameof(NearKeyChange)])]
    public void Cleanup()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(FarKeyChangeWithSubscriber))]
    public void CleanupSubscribed()
    {
        orderBy.CollectionChanged -= OrderByCollectionChanged;
        Cleanup();
    }
}
