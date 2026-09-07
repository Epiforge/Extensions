namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class IndexerPricingBenchmarks
{
    const int elementCount = 1000;

    static readonly ObservableRangeCollection<int> observableList = BuildObservableList();
    static readonly List<int> plainList = BuildPlainList();
    static readonly ObservableDictionary<int, int> table = BuildTable();

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> dictionaryRead = person => table[person.Rank] > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> observableListRead = person => observableList[person.Rank] > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> plainListRead = person => plainList[person.Rank] > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    static ObservableRangeCollection<int> BuildObservableList()
    {
        var built = new ObservableRangeCollection<int>();
        for (var i = 0; i < elementCount; ++i)
            built.Add(i);
        return built;
    }

    static List<int> BuildPlainList()
    {
        var built = new List<int>();
        for (var i = 0; i < elementCount; ++i)
            built.Add(i);
        return built;
    }

    static ObservableDictionary<int, int> BuildTable()
    {
        var built = new ObservableDictionary<int, int>();
        for (var i = 0; i < elementCount; ++i)
            built.Add(i, i);
        return built;
    }

    CollectionObserver direct = null!;
    CollectionObserver graph = null!;
    ObservableRangeCollection<BenchmarkPersonWithPartner> source = null!;

    [Benchmark]
    public void DictionaryReadDirect() =>
        ConstructAndDispose(direct, dictionaryRead);

    [Benchmark]
    public void DictionaryReadGraph() =>
        ConstructAndDispose(graph, dictionaryRead);

    [Benchmark]
    public void ObservableListReadDirect() =>
        ConstructAndDispose(direct, observableListRead);

    [Benchmark]
    public void ObservableListReadGraph() =>
        ConstructAndDispose(graph, observableListRead);

    [Benchmark]
    public void PlainListReadDirect() =>
        ConstructAndDispose(direct, plainListRead);

    [Benchmark]
    public void PlainListReadGraph() =>
        ConstructAndDispose(graph, plainListRead);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    void ConstructAndDispose(CollectionObserver observer, Expression<Func<BenchmarkPersonWithPartner, bool>> predicate)
    {
        var sourceQuery = observer.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(predicate);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        graph = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false }));
        source = BenchmarkPersonWithPartner.CreateCollection(elementCount);
    }
}
