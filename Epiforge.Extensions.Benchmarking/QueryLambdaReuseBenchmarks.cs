namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices the lambdas the queries build for themselves, by building one query over each of many sources and disposing them all, against the same operators given lambdas the caller holds
/// </summary>
/// <remarks>
/// GS5 measured <c>ObserveCast</c> compiling afresh on every call, because the lambda it observes is built anew each time and the observer's caches match by reference. Each arm builds one query over each of 256 standing sources of one element, as GS5's traversal does over time slices, and then disposes all of them, under the optimizer GS5 configures. <see cref="WhereShared"/> and <see cref="WhereRebuilt"/> differ only by the caller rebuilding its predicate for every call, which prices one pass of the optimizer, the analysis and the compiler per source in the same run; the other arms are multiples of that difference or controls
/// </remarks>
[MemoryDiagnoser]
public class QueryLambdaReuseBenchmarks
{
    const int SourceCount = 256;

    static readonly Expression<Func<BenchmarkPerson, object>> asObject = person => person;
    static readonly Expression<Func<BenchmarkPerson, string>> name = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, int>> rank = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, IComparable>> rankComparable = person => person.Rank;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsNonNegative = person => person.Rank >= 0;
    static readonly Expression<Func<int, BenchmarkPerson, bool>> valueRankIsNonNegative = (key, person) => person.Rank >= 0;

    readonly IDisposable[] built = new IDisposable[SourceCount];
    IObservableCollectionQuery<BenchmarkPerson>[] collections = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson>[] dictionaries = null!;
    readonly List<IDisposable> standing = [];

    [Benchmark]
    public void AllShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveAll(rankIsNonNegative);
        Release();
    }

    [Benchmark]
    public void Cast()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveCast<object>();
        Release();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark]
    public void DictionaryCast()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = dictionaries[i].ObserveCast<object>();
        Release();
    }

    [Benchmark]
    public void DictionaryWhereShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = dictionaries[i].ObserveWhere(valueRankIsNonNegative);
        Release();
    }

    [Benchmark]
    public void GroupByShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveGroupBy(rank);
        Release();
    }

    [Benchmark]
    public void MaxByShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveMaxBy(rank);
        Release();
    }

    [Benchmark]
    public void OfType()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveOfType<object>();
        Release();
    }

    [Benchmark]
    public void OrderByRebuilt()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveOrderBy(person => person.Rank);
        Release();
    }

    [Benchmark]
    public void OrderByShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveOrderBy(rankComparable);
        Release();
    }

    void Release()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i].Dispose();
    }

    [Benchmark]
    public void SelectShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveSelect(asObject);
        Release();
    }

    [GlobalSetup]
    public void Setup()
    {
        var collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit }));
        collections = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        dictionaries = new IObservableDictionaryQuery<int, BenchmarkPerson>[SourceCount];
        for (var i = 0; i < SourceCount; ++i)
        {
            var person = new BenchmarkPerson($"P{i}", i);
            collections[i] = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>([person])));
            dictionaries[i] = Stand(collectionObserver.ObserveReadOnlyDictionary(new ObservableDictionary<int, BenchmarkPerson>(new Dictionary<int, BenchmarkPerson> { [i] = person })));
        }
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }

    [Benchmark]
    public void ToDictionaryShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveToDictionary(name);
        Release();
    }

    [Benchmark]
    public void ToLookupShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveToLookup(rank);
        Release();
    }

    [Benchmark]
    public void WhereRebuilt()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveWhere(person => person.Rank >= 0);
        Release();
    }

    [Benchmark(Baseline = true)]
    public void WhereShared()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveWhere(rankIsNonNegative);
        Release();
    }
}
