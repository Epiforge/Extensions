namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices building and disposing each operator GS5's traversal builds for a time slice, over sources of zero and one element, and the whole chain it builds for one
/// </summary>
/// <remarks>
/// GS5 builds about eight queries for each time slice, and most of its slices hold one trait value and no list values. Each arm builds over each of 256 standing sources, none of which shares a query with another, with lambdas held in static fields so that no arm pays the compiler or an optimizer miss, and then disposes everything it built. <see cref="Root"/> builds a source query over a collection nothing else observes. <see cref="Slice"/> builds what <c>TraverseTraitValues</c> builds from a lookup's grouping: a cast, a filter, a projection of what passed, two flattenings of that projection and two concatenations
/// </remarks>
[MemoryDiagnoser]
public class OperatorConstructionBenchmarks
{
    const int SourceCount = 256;
    const int SliceQueries = 7;

    static readonly Expression<Func<BenchmarkPerson, IEnumerable<object>>> emptyArray = person => Array.Empty<object>();
    static readonly Expression<Func<BenchmarkPerson, IEnumerable<object>>> emptyEnumerable = person => Enumerable.Empty<object>();
    static readonly Expression<Func<BenchmarkPerson, BenchmarkPerson>> itself = person => person;
    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsNonNegative = person => person.Rank >= 0;

    readonly IDisposable[] built = new IDisposable[SourceCount * SliceQueries];
    CollectionObserver collectionObserver = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] collections = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] seconds = null!;
    readonly List<IDisposable> standing = [];
    ObservableRangeCollection<BenchmarkPerson>[] unobserved = null!;

    [Params(0, 1)]
    public int Elements { get; set; }

    [Benchmark]
    public void Cast()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveCast<object>();
        Release(SourceCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark]
    public void Concat()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveConcat(seconds[i]);
        Release(SourceCount);
    }

    ObservableRangeCollection<BenchmarkPerson> CreateCollection(int index)
    {
        var people = new List<BenchmarkPerson>(Elements);
        for (var e = 0; e < Elements; ++e)
            people.Add(new BenchmarkPerson($"P{index}", index));
        return new ObservableRangeCollection<BenchmarkPerson>(people);
    }

    void Release(int count)
    {
        for (var i = count - 1; i >= 0; --i)
            built[i].Dispose();
    }

    [Benchmark]
    public void Root()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collectionObserver.ObserveReadOnlyList(unobserved[i]);
        Release(SourceCount);
    }

    [Benchmark]
    public void Select()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveSelect(itself);
        Release(SourceCount);
    }

    [Benchmark]
    public void SelectMany()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveSelectMany(emptyArray);
        Release(SourceCount);
    }

    [GlobalSetup]
    public void Setup()
    {
        collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit }));
        collections = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        seconds = new IObservableCollectionQuery<BenchmarkPerson>[SourceCount];
        unobserved = new ObservableRangeCollection<BenchmarkPerson>[SourceCount];
        for (var i = 0; i < SourceCount; ++i)
        {
            collections[i] = Stand(collectionObserver.ObserveReadOnlyList(CreateCollection(i)));
            seconds[i] = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>()));
            unobserved[i] = CreateCollection(i);
        }
    }

    [Benchmark]
    public void Slice()
    {
        var b = 0;
        for (var i = 0; i < SourceCount; ++i)
        {
            var source = collections[i];
            var associated = source.ObserveCast<object>();
            var references = source.ObserveWhere(rankIsNonNegative);
            var values = references.ObserveSelect(itself);
            var up = values.ObserveSelectMany(emptyArray);
            var traitValues = values.ObserveSelectMany(emptyEnumerable);
            var withUp = associated.ObserveConcat(up);
            built[b++] = associated;
            built[b++] = references;
            built[b++] = values;
            built[b++] = up;
            built[b++] = traitValues;
            built[b++] = withUp;
            built[b++] = withUp.ObserveConcat(traitValues);
        }
        Release(b);
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }

    [Benchmark(Baseline = true)]
    public void Where()
    {
        for (var i = 0; i < SourceCount; ++i)
            built[i] = collections[i].ObserveWhere(rankIsNonNegative);
        Release(SourceCount);
    }
}
