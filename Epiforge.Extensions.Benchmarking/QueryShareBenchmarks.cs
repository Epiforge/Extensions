namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what each query shape costs for every element beyond the observation it makes for that element, by giving each the same source and a selector or predicate whose observation is already priced on its own in <see cref="ObservationConstructionBenchmarks" />. Ordering is not among them: its key selector is typed to <see cref="IComparable" />, so its observation is not the one priced there
/// </summary>
[MemoryDiagnoser]
public class QueryShareBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPersonWithPartner, int>> rankRead = person => person.Rank;
    static readonly Expression<Func<BenchmarkPersonWithPartner, bool>> rankComparison = person => person.Rank > 0;

    CollectionObserver direct = null!;
    ObservableRangeCollection<BenchmarkPersonWithPartner> source = null!;

    [Benchmark]
    public void SelectDirect()
    {
        var sourceQuery = direct.ObserveReadOnlyList(source);
        var select = sourceQuery.ObserveSelect(rankRead);
        select.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void SourceQueryOnly()
    {
        var sourceQuery = direct.ObserveReadOnlyList(source);
        sourceQuery.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void WhereDirect()
    {
        var sourceQuery = direct.ObserveReadOnlyList(source);
        var where = sourceQuery.ObserveWhere(rankComparison);
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        direct = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = true }));
        source = BenchmarkPersonWithPartner.CreateCollection(elementCount);
    }
}
