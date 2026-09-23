namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices creating and disposing many scoped wrappers over one shared query, in the order they were created and in reverse
/// </summary>
/// <remarks>
/// GS5 asks one shared query for a lookup once per time slice and disposes the wrappers it is handed in the order it received them. Each wrapper attaches handlers of its own to the shared query when it is made, whether or not anything subscribes to the wrapper, and removing a handler from a delegate list searches it from the end and copies what remains. A standing wrapper keeps each shared query alive between operations, so that every arm measures wrappers and nothing else
/// </remarks>
[MemoryDiagnoser]
public class ScopedWrapperDisposalBenchmarks
{
    static readonly Expression<Func<int, int>> identity = n => n;
    static readonly Expression<Func<int, bool>> isPositive = n => n > 0;

    readonly IDisposable[] built = new IDisposable[4096];
    IObservableCollectionQuery<int> source = null!;
    readonly List<IDisposable> standing = [];

    void Build(int count, bool lookup)
    {
        for (var i = 0; i < count; ++i)
        {
            if (lookup)
                built[i] = source.ObserveToLookup(identity);
            else
                built[i] = source.ObserveWhere(isPositive);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = standing.Count - 1; i >= 0; --i)
            standing[i].Dispose();
        standing.Clear();
    }

    void DisposeInOrder(int count)
    {
        for (var i = 0; i < count; ++i)
            built[i].Dispose();
    }

    void DisposeInReverse(int count)
    {
        for (var i = count - 1; i >= 0; --i)
            built[i].Dispose();
    }

    [Benchmark]
    public void LookupsDisposedInOrder1024()
    {
        Build(1024, true);
        DisposeInOrder(1024);
    }

    [Benchmark]
    public void LookupsDisposedInOrder256()
    {
        Build(256, true);
        DisposeInOrder(256);
    }

    [Benchmark(Baseline = true)]
    public void LookupsDisposedInOrder4096()
    {
        Build(4096, true);
        DisposeInOrder(4096);
    }

    [Benchmark]
    public void LookupsDisposedInReverse4096()
    {
        Build(4096, true);
        DisposeInReverse(4096);
    }

    [GlobalSetup]
    public void Setup()
    {
        var collectionObserver = new CollectionObserver(new ExpressionObserver(new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit }));
        source = collectionObserver.ObserveReadOnlyList(new ObservableRangeCollection<int>(Enumerable.Range(1, 16)));
        standing.Add(source);
        standing.Add(source.ObserveToLookup(identity));
        standing.Add(source.ObserveWhere(isPositive));
    }

    [Benchmark]
    public void WheresDisposedInOrder4096()
    {
        Build(4096, false);
        DisposeInOrder(4096);
    }
}
