namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a raise against many observations of one object, including raises for a property none of them watches
/// </summary>
/// <remarks>
/// Both mechanisms register once with a source's event and keep their own list of what is attached to it, so a raise walks one entry of the object's invocation list either way and then one list of their own. Where they part is what that walk does: the fast path looks for the first attachment which wants the name reported and returns before entering a propagation where none does, while the graph invokes every attachment's handler and each of them decides for itself and returns. What that is worth has never been measured, and the shape which makes it matter is an entity announcing several properties for one change while an observation watches one of them
/// </remarks>
/// <remarks>
/// The subject passes pre-allocated arguments, so a raise allocates nothing and what these arms report is the mechanisms' own cost rather than a floor the subject sets. Every observation is constructed once, outside the measurement
/// </remarks>
[MemoryDiagnoser]
public class NotificationFanOutBenchmarks
{
    const int observationCount = 1000;
    const int raises = 1000;

    static readonly Expression<Func<BenchmarkTicker, bool>> watchesEnd = ticker => ticker.End > 0;

    IObservableExpression<BenchmarkTicker, bool>[] aDefaultObservations = null!;
    ExpressionObserver aDefaultObserver = null!;
    IObservableExpression<BenchmarkTicker, bool>[] graphObservations = null!;
    ExpressionObserver graphObserver = null!;
    BenchmarkTicker aDefaultTicker = null!;
    BenchmarkTicker graphTicker = null!;

    [Benchmark(Baseline = true)]
    public void WatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.End = i + 1;
    }

    [Benchmark]
    public void WatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.End = i + 1;
    }

    [Benchmark]
    public void UnwatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.Idle = i + 1;
    }

    [Benchmark]
    public void UnwatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.Idle = i + 1;
    }

    [Benchmark]
    public void MultiRaiseDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.Advance(1);
    }

    [Benchmark]
    public void MultiRaiseGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.Advance(1);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < observationCount; ++i)
        {
            aDefaultObservations[i].Dispose();
            graphObservations[i].Dispose();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefaultObserver = new ExpressionObserver();
        graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        aDefaultTicker = new BenchmarkTicker();
        graphTicker = new BenchmarkTicker();
        aDefaultObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        graphObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            aDefaultObservations[i] = aDefaultObserver.Observe(watchesEnd, aDefaultTicker);
            graphObservations[i] = graphObserver.Observe(watchesEnd, graphTicker);
        }
    }
}
