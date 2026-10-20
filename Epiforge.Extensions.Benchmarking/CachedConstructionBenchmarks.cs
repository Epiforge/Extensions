namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices observing an expression on the graph and building a projection, both where nothing like it is cached, which builds one, and where one is, which shares it, so that what caching a node or a query only once it is built costs either way is measured against the release which cached them before building them
/// </summary>
/// <remarks>
/// Each arm observes or builds over each of 256 standing subjects or sources and then disposes everything it made. The sharing arms each have an observation or a projection standing beside them for the whole run, so that each of theirs shares that one; the building arms have nothing standing beside them, so that each of theirs builds its own. The graph arms observe with direct subscription off, since a direct observation is never cached
/// </remarks>
[AgainstReleasedExpressions("7.1.3")]
[MemoryDiagnoser]
public class CachedConstructionBenchmarks
{
    const int Count = 256;

    static readonly Expression<Func<BenchmarkPerson, int>> doubledRank = person => person.Rank * 2;
    static readonly Expression<Func<BenchmarkPerson, int>> rank = person => person.Rank;

    readonly IDisposable[] built = new IDisposable[Count];
    IObservableCollectionQuery<BenchmarkPerson>[] buildingSources = null!;
    BenchmarkPerson[] buildingSubjects = null!;
    ExpressionObserver graphObserver = null!;
    IObservableCollectionQuery<BenchmarkPerson>[] sharingSources = null!;
    BenchmarkPerson[] sharingSubjects = null!;
    readonly List<IDisposable> standing = [];

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    [Benchmark(Baseline = true)]
    public void GraphBuilding()
    {
        for (var i = 0; i < Count; ++i)
            built[i] = graphObserver.Observe(doubledRank, buildingSubjects[i]);
        Release();
    }

    [Benchmark]
    public void GraphSharing()
    {
        for (var i = 0; i < Count; ++i)
            built[i] = graphObserver.Observe(doubledRank, sharingSubjects[i]);
        Release();
    }

    [Benchmark]
    public void ProjectionBuilding()
    {
        for (var i = 0; i < Count; ++i)
            built[i] = buildingSources[i].ObserveSelect(rank);
        Release();
    }

    [Benchmark]
    public void ProjectionSharing()
    {
        for (var i = 0; i < Count; ++i)
            built[i] = sharingSources[i].ObserveSelect(rank);
        Release();
    }

    void Release()
    {
        for (var i = 0; i < Count; ++i)
            built[i].Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var collectionObserver = new CollectionObserver(new ExpressionObserver());
        buildingSubjects = new BenchmarkPerson[Count];
        sharingSubjects = new BenchmarkPerson[Count];
        buildingSources = new IObservableCollectionQuery<BenchmarkPerson>[Count];
        sharingSources = new IObservableCollectionQuery<BenchmarkPerson>[Count];
        for (var i = 0; i < Count; ++i)
        {
            buildingSubjects[i] = new BenchmarkPerson($"B{i}", i);
            sharingSubjects[i] = new BenchmarkPerson($"S{i}", i);
            Stand(graphObserver.Observe(doubledRank, sharingSubjects[i]));
            buildingSources[i] = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>([new BenchmarkPerson($"B{i}", i)])));
            sharingSources[i] = Stand(collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<BenchmarkPerson>([new BenchmarkPerson($"S{i}", i)])));
            Stand(sharingSources[i].ObserveSelect(rank));
        }
    }

    T Stand<T>(T disposable)
        where T : IDisposable
    {
        standing.Add(disposable);
        return disposable;
    }
}
