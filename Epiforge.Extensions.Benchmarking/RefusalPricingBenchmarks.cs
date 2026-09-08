namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class RefusalPricingBenchmarks
{
    const int elementCount = 1000;

    static readonly BenchmarkPersonWithPartner other = new("other", 0);
    static readonly ObservableDictionary<int, int> table = BuildTable();

    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> conditional = person => (person.Rank > 0 ? person.Rank : person.Rank) > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> constantPredicate = person => true;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> indexerRead = person => table[person.Rank] > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> notifyingChain = person => person.Partner!.Rank > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> sharedSourceRepeated = person => other.Rank + other.Rank > person.Rank;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> twoObjectConditional = person => (person.Rank > 0 ? other.Rank : person.Rank) > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> twoObjectShortCircuit = person => person.Rank > 0 && other.Rank > 0;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> twoSourceSum = person => other.Rank + person.Rank > 0;

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
    public void ConditionalDirect() =>
        ConstructAndDispose(direct, conditional);

    [Benchmark]
    public void ConditionalGraph() =>
        ConstructAndDispose(graph, conditional);

    [Benchmark]
    public void ConstantPredicateDirect() =>
        ConstructAndDispose(direct, constantPredicate);

    [Benchmark]
    public void IndexerReadDirect() =>
        ConstructAndDispose(direct, indexerRead);

    [Benchmark]
    public void IndexerReadGraph() =>
        ConstructAndDispose(graph, indexerRead);

    [Benchmark]
    public void NotifyingChainDirect() =>
        ConstructAndDispose(direct, notifyingChain);

    [Benchmark]
    public void NotifyingChainGraph() =>
        ConstructAndDispose(graph, notifyingChain);

    [Benchmark]
    public void RankComparisonDirect() =>
        ConstructAndDispose(direct, rankComparison);

    [Benchmark(Baseline = true)]
    public void RankComparisonGraph() =>
        ConstructAndDispose(graph, rankComparison);

    [Benchmark]
    public void SharedSourceRepeatedDirect() =>
        ConstructAndDispose(direct, sharedSourceRepeated);

    [Benchmark]
    public void SharedSourceRepeatedGraph() =>
        ConstructAndDispose(graph, sharedSourceRepeated);

    [Benchmark]
    public void SourceQueryOnly()
    {
        var sourceQuery = direct.ObserveReadOnlyList(source);
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void TwoObjectConditionalDirect() =>
        ConstructAndDispose(direct, twoObjectConditional);

    [Benchmark]
    public void TwoObjectConditionalGraph() =>
        ConstructAndDispose(graph, twoObjectConditional);

    [Benchmark]
    public void TwoObjectShortCircuitDirect() =>
        ConstructAndDispose(direct, twoObjectShortCircuit);

    [Benchmark]
    public void TwoObjectShortCircuitGraph() =>
        ConstructAndDispose(graph, twoObjectShortCircuit);

    [Benchmark]
    public void TwoSourceSumDirect() =>
        ConstructAndDispose(direct, twoSourceSum);

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
