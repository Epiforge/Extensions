namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what each way of synchronizing a query adds to a change of what it is built over
/// </summary>
/// <remarks>
/// Each arm adds an element at the end of a thousand and removes it again. The unobserved arm prices the list's own work and the observed arm an observation of it and nothing else; every other arm adds one synchronized query over that observation. The context runs whatever it is sent or posted at once, on the thread which sent or posted it, and the callback takes a lock around what it is given, so that the arms price the queries' own work rather than a thread switch. A consistent query synchronized with a context is changed from a thread other than the context's in <see cref="UsingSynchronizationContext"/> and from the context's own in <see cref="UsingSynchronizationContextFromItsThread"/>, which makes the context current around each change and so adds the cost of doing that. Every synchronized query is verified before it is measured
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class SynchronizedQueryBenchmarks
{
    const int elementCount = 1000;

    /// <summary>
    /// A context which runs what it is sent or posted at once, on the calling thread
    /// </summary>
    sealed class ImmediateSynchronizationContext :
        SynchronizationContext
    {
        public override SynchronizationContext CreateCopy() =>
            this;

        public override void Post(SendOrPostCallback d, object? state) =>
            d(state);

        public override void Send(SendOrPostCallback d, object? state) =>
            d(state);
    }

    readonly ImmediateSynchronizationContext context = new();
    IObservableCollectionQuery<int>? observed;
    ObservableRangeCollection<int> source = null!;
    IObservableCollectionQuery<int>? synchronized;
    readonly object syncRoot = new();

    static void Callback(IEnumerable collection, object context, Action accessMethod, bool writeAccess)
    {
        lock (context)
            accessMethod();
    }

    void Change()
    {
        source.Add(elementCount);
        source.RemoveAt(elementCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        synchronized?.Dispose();
        synchronized = null;
        observed?.Dispose();
        observed = null;
    }

    [Benchmark]
    public void Observed() =>
        Change();

    void Probe()
    {
        source.Add(elementCount);
        if (synchronized!.Count != elementCount + 1 || synchronized[elementCount] != elementCount)
            throw new InvalidOperationException($"{synchronized} did not follow an addition");
        source.RemoveAt(elementCount);
        if (synchronized.Count != elementCount || !synchronized.SequenceEqual(source))
            throw new InvalidOperationException($"{synchronized} did not follow a removal");
    }

    [GlobalSetup(Target = nameof(Observed))]
    public void SetupObserved()
    {
        SetupSource();
        observed = new CollectionObserver().ObserveReadOnlyList(source);
    }

    void SetupSource() =>
        source = new ObservableRangeCollection<int>(Enumerable.Range(0, elementCount));

    void SetupSynchronized(Func<IObservableCollectionQuery<int>, IObservableCollectionQuery<int>> synchronize)
    {
        SetupObserved();
        synchronized = synchronize(observed!);
        Probe();
    }

    [GlobalSetup(Target = nameof(Unobserved))]
    public void SetupUnobserved() =>
        SetupSource();

    [GlobalSetup(Target = nameof(UsingSynchronizationCallback))]
    public void SetupUsingSynchronizationCallback() =>
        SetupSynchronized(query => query.ObserveUsingSynchronizationCallback(syncRoot, Callback));

    [GlobalSetup(Targets = [nameof(UsingSynchronizationContext), nameof(UsingSynchronizationContextFromItsThread)])]
    public void SetupUsingSynchronizationContext() =>
        SetupSynchronized(query => query.ObserveUsingSynchronizationContext(context));

    [GlobalSetup(Target = nameof(UsingSynchronizationContextEventually))]
    public void SetupUsingSynchronizationContextEventually() =>
        SetupSynchronized(query => query.ObserveUsingSynchronizationContextEventually(context));

    [GlobalSetup(Target = nameof(UsingSyncRoot))]
    public void SetupUsingSyncRoot() =>
        SetupSynchronized(query => query.ObserveUsingSyncRoot(syncRoot));

    [Benchmark(Baseline = true)]
    public void Unobserved() =>
        Change();

    [Benchmark]
    public void UsingSynchronizationCallback() =>
        Change();

    [Benchmark]
    public void UsingSynchronizationContext() =>
        Change();

    [Benchmark]
    public void UsingSynchronizationContextEventually() =>
        Change();

    [Benchmark]
    public void UsingSynchronizationContextFromItsThread()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        Change();
        SynchronizationContext.SetSynchronizationContext(previous);
    }

    [Benchmark]
    public void UsingSyncRoot() =>
        Change();
}
