namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what an observer spends disposing of the values its observations make and replace, when those values are disposed of asynchronously and the observer waits for each disposal, does not wait, or disposes of them synchronously
/// </summary>
/// <remarks>
/// A thousand standing observations each make a value from a person's rank, and every operation changes every rank once, so that every observation makes a value and disposes of the one it replaces. Each value's disposal finishes before it returns, which is the common case and the one in which waiting for it should cost least. Each standing arm verifies in setup that every replaced value is disposed of
/// </remarks>
[AgainstReleasedExpressions("7.0.0")]
[MemoryDiagnoser]
public class BlockingAsyncDisposalBenchmarks
{
    const int observationCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, AsyncValue>> makeAsyncValue = person => new AsyncValue(person.Rank);
    static readonly Expression<Func<BenchmarkPerson, SyncValue>> makeSyncValue = person => new SyncValue(person.Rank);

    /// <summary>
    /// A value disposed of only asynchronously, whose disposal finishes before it returns
    /// </summary>
    public sealed class AsyncValue(int value) :
        IAsyncDisposable
    {
        public static int Disposals;

        public int Value { get; } = value;

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref Disposals);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A value disposed of synchronously
    /// </summary>
    public sealed class SyncValue(int value) :
        IDisposable
    {
        public static int Disposals;

        public int Value { get; } = value;

        public void Dispose() =>
            Interlocked.Increment(ref Disposals);
    }

    readonly List<IDisposable> observations = [];
    BenchmarkPerson[] people = null!;

    void ChangeEveryRank()
    {
        for (var i = 0; i < observationCount; ++i)
            people[i].Rank ^= 2;
    }

    /// <summary>
    /// What changing every rank costs before anything observes it, which is the floor every arm below stands on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = observations.Count - 1; i >= 0; --i)
            observations[i].Dispose();
        observations.Clear();
    }

    [Benchmark]
    public void ReplaceEveryAsyncValueNotWaiting() =>
        ChangeEveryRank();

    [Benchmark]
    public void ReplaceEveryAsyncValueWaiting() =>
        ChangeEveryRank();

    [Benchmark]
    public void ReplaceEverySyncValue() =>
        ChangeEveryRank();

    void Setup<TValue>(Expression<Func<BenchmarkPerson, TValue>> make, bool blockOnAsyncDisposal, Func<int> disposals)
    {
        SetupPeople();
        var options = new ExpressionObserverOptions { BlockOnAsyncDisposal = blockOnAsyncDisposal };
        options.AddConstructedTypeDisposal(typeof(TValue), typeof(int));
        var observer = new ExpressionObserver(options);
        for (var i = 0; i < observationCount; ++i)
            observations.Add(observer.Observe(make, people[i]));
        var before = disposals();
        ChangeEveryRank();
        if (!SpinWait.SpinUntil(() => disposals() - before == observationCount, TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException($"{observationCount} values were replaced and {disposals() - before} disposed of");
    }

    [GlobalSetup(Target = nameof(ReplaceEveryAsyncValueNotWaiting))]
    public void SetupAsyncValueNotWaiting() =>
        Setup(makeAsyncValue, false, () => Volatile.Read(ref AsyncValue.Disposals));

    [GlobalSetup(Target = nameof(ReplaceEveryAsyncValueWaiting))]
    public void SetupAsyncValueWaiting() =>
        Setup(makeAsyncValue, true, () => Volatile.Read(ref AsyncValue.Disposals));

    void SetupPeople()
    {
        people = new BenchmarkPerson[observationCount];
        for (var i = 0; i < observationCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i);
    }

    [GlobalSetup(Target = nameof(ReplaceEverySyncValue))]
    public void SetupSyncValue() =>
        Setup(makeSyncValue, false, () => Volatile.Read(ref SyncValue.Disposals));

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        SetupPeople();
}
