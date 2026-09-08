namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Asks whether an ordered query's cost for one key change grows with the number of elements because it evaluates more or because each evaluation costs more, by taking the same single change at three sizes
/// </summary>
[MemoryDiagnoser]
public class OrderByKeyChangeScalingBenchmarks
{
    static readonly Expression<Func<BenchmarkPerson, IComparable>> rankKey = person => person.Rank;

    int cursor;
    bool[] flipped = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<BenchmarkPerson> orderBy = null!;
    int[] ranks = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    [Params(250, 1000, 4000)]
    public int ElementCount { get; set; }

    [Benchmark]
    public void KeyChange()
    {
        var person = source[cursor];
        person.Rank = flipped[cursor] ? ranks[cursor] : ElementCount - 1 - ranks[cursor];
        flipped[cursor] = !flipped[cursor];
        cursor = cursor + 1 == ElementCount ? 0 : cursor + 1;
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        source = BenchmarkPerson.CreateCollection(ElementCount);
        flipped = new bool[ElementCount];
        ranks = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            ranks[i] = source[i].Rank;
        sourceQuery = observer.ObserveReadOnlyList(source);
        orderBy = sourceQuery.ObserveOrderBy(rankKey);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        orderBy.Dispose();
        sourceQuery.Dispose();
    }
}
