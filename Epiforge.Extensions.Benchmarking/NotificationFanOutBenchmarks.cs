namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a raise against many observations of one object, including raises for a property none of them watches
/// </summary>
/// <remarks>
/// Both mechanisms register once with a source's event and keep their own list of what is attached to it, so a raise walks one entry of the object's invocation list either way and then one list of their own. Where they part is what that walk does: the fast path looks for the first attachment which wants the name reported and returns before entering a propagation where none does, while the graph invokes every attachment's handler and each of them decides for itself and returns
/// </remarks>
/// <remarks>
/// How long each list is turns on something else entirely, which is what these arms report. The graph caches a node per expression, so many observations of one expression over one object share one graph and one attachment, where the fast path caches nothing across observations and attaches one apiece: the shared arms compare one attachment against a thousand
/// </remarks>
/// <remarks>
/// The distinct arms were meant to stop that by giving every observation its own constant, and they do not. The read of the property is the identical subexpression in all of them and only the comparison above it differs, so the graph still serves one member node to all thousand. Its allocation says so: twenty-four thousand bytes on the distinct watched arm, the same as on the shared one, which is one boxed value per raise rather than one per observation. What the distinct arms compare is therefore a thousand whole-delegate invocations against one shared read feeding a thousand comparisons, which is a real difference and worth measuring, but it is not the dispatch difference described above. That difference cannot be read from this instrument at all, because no arm gives the graph more than one attachment on the source
/// </remarks>
/// <remarks>
/// The subject passes pre-allocated arguments, so a raise allocates nothing and what these arms report is the mechanisms' own cost rather than a floor the subject sets. Every observation is constructed once, outside the measurement
/// </remarks>
[MemoryDiagnoser]
public class NotificationFanOutBenchmarks
{
    const int observationCount = 1000;
    const int raises = 1000;

    static readonly PropertyInfo endProperty = typeof(BenchmarkTicker).GetProperty(nameof(BenchmarkTicker.End))!;

    static readonly Expression<Func<BenchmarkTicker, bool>> watchesEnd = ticker => ticker.End > 0;

    /// <summary>
    /// Yields an expression watching the same property as every other and equal to none of them, so that neither mechanism can serve two observations of it from one node
    /// </summary>
    static Expression<Func<BenchmarkTicker, bool>> WatchesEndDistinctly(int which)
    {
        var ticker = Expression.Parameter(typeof(BenchmarkTicker), "ticker");
        return Expression.Lambda<Func<BenchmarkTicker, bool>>(Expression.GreaterThan(Expression.MakeMemberAccess(ticker, endProperty), Expression.Constant((long)-which - 1)), ticker);
    }

    IObservableExpression<BenchmarkTicker, bool>[] aDefaultDistinctObservations = null!;
    IObservableExpression<BenchmarkTicker, bool>[] aDefaultObservations = null!;
    ExpressionObserver aDefaultObserver = null!;
    IObservableExpression<BenchmarkTicker, bool>[] graphDistinctObservations = null!;
    IObservableExpression<BenchmarkTicker, bool>[] graphObservations = null!;
    ExpressionObserver graphObserver = null!;
    BenchmarkTicker aDefaultDistinctTicker = null!;
    BenchmarkTicker aDefaultTicker = null!;
    BenchmarkTicker graphDistinctTicker = null!;
    BenchmarkTicker graphTicker = null!;

    [Benchmark(Baseline = true)]
    public void SharedWatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.End = i + 1;
    }

    [Benchmark]
    public void SharedWatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.End = i + 1;
    }

    [Benchmark]
    public void SharedUnwatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.Idle = i + 1;
    }

    [Benchmark]
    public void SharedUnwatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.Idle = i + 1;
    }

    [Benchmark]
    public void SharedMultiRaiseDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultTicker.Advance(1);
    }

    [Benchmark]
    public void SharedMultiRaiseGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphTicker.Advance(1);
    }

    [Benchmark]
    public void DistinctWatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultDistinctTicker.End = i + 1;
    }

    [Benchmark]
    public void DistinctWatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphDistinctTicker.End = i + 1;
    }

    [Benchmark]
    public void DistinctUnwatchedPropertyDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultDistinctTicker.Idle = i + 1;
    }

    [Benchmark]
    public void DistinctUnwatchedPropertyGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphDistinctTicker.Idle = i + 1;
    }

    [Benchmark]
    public void DistinctMultiRaiseDefault()
    {
        for (var i = 0; i < raises; ++i)
            aDefaultDistinctTicker.Advance(1);
    }

    [Benchmark]
    public void DistinctMultiRaiseGraph()
    {
        for (var i = 0; i < raises; ++i)
            graphDistinctTicker.Advance(1);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < observationCount; ++i)
        {
            aDefaultObservations[i].Dispose();
            aDefaultDistinctObservations[i].Dispose();
            graphObservations[i].Dispose();
            graphDistinctObservations[i].Dispose();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefaultObserver = new ExpressionObserver();
        graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        aDefaultTicker = new BenchmarkTicker();
        graphTicker = new BenchmarkTicker();
        aDefaultDistinctTicker = new BenchmarkTicker();
        graphDistinctTicker = new BenchmarkTicker();
        aDefaultObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        aDefaultDistinctObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        graphObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        graphDistinctObservations = new IObservableExpression<BenchmarkTicker, bool>[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            var distinct = WatchesEndDistinctly(i);
            aDefaultObservations[i] = aDefaultObserver.Observe(watchesEnd, aDefaultTicker);
            aDefaultDistinctObservations[i] = aDefaultObserver.Observe(distinct, aDefaultDistinctTicker);
            graphObservations[i] = graphObserver.Observe(watchesEnd, graphTicker);
            graphDistinctObservations[i] = graphObserver.Observe(distinct, graphDistinctTicker);
        }
    }
}
