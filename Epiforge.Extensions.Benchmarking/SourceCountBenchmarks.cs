namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices construction and raises in the shape an application laying out a grid has, which is many source objects carrying a few attachments each
/// </summary>
/// <remarks>
/// <see cref="NotificationFanOutBenchmarks" /> measures the opposite shape, one object carrying a thousand attachments, and every conclusion about how a source finds the attachments which want a reported name has so far been drawn from it. A structure which makes that search cheap when the list is long can be pure overhead when the list is six, and it is paid on construction rather than on notification, which is the path a grid spends its time on. These arms exist so that a change justified by the fan-out instrument has somewhere to be disproved
/// </remarks>
/// <remarks>
/// Six observations of each source, each watching a different property of it, which is a row of six columns. One raise is of the property one of the six watches and five do not; the other is of a property none of them watches. The subject passes pre-allocated arguments, so a raise allocates nothing of its own
/// </remarks>
[MemoryDiagnoser]
public class SourceCountBenchmarks
{
    const int columnCount = 6;
    const int raisePasses = 100;
    const int sourceCount = 1000;

    static readonly Expression<Func<BenchmarkSlice, bool>>[] columns =
    [
        slice => slice.Amount > 0,
        slice => slice.Duration > 0,
        slice => slice.End > 0,
        slice => slice.Rate > 0,
        slice => slice.Start > 0,
        slice => slice.Weight > 0
    ];

    static ExpressionObserver Observer(bool useDirectSubscription) =>
        useDirectSubscription ? new ExpressionObserver() : new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });

    static void ConstructAndDispose(ExpressionObserver observer, BenchmarkSlice[] slices)
    {
        var observations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * columnCount];
        for (var i = 0; i < sourceCount; ++i)
            for (var column = 0; column < columnCount; ++column)
                observations[i * columnCount + column] = observer.Observe(columns[column], slices[i]);
        for (var i = 0; i < observations.Length; ++i)
            observations[i].Dispose();
    }

    IObservableExpression<BenchmarkSlice, bool>[] aDefaultObservations = null!;
    ExpressionObserver aDefaultObserver = null!;
    BenchmarkSlice[] aDefaultSlices = null!;
    ExpressionObserver aDefaultConstructionObserver = null!;
    BenchmarkSlice[] aDefaultConstructionSlices = null!;
    IObservableExpression<BenchmarkSlice, bool>[] graphObservations = null!;
    ExpressionObserver graphObserver = null!;
    BenchmarkSlice[] graphSlices = null!;
    ExpressionObserver graphConstructionObserver = null!;
    BenchmarkSlice[] graphConstructionSlices = null!;

    [Benchmark(Baseline = true)]
    public void ManySourcesConstructDefault() =>
        ConstructAndDispose(aDefaultConstructionObserver, aDefaultConstructionSlices);

    [Benchmark]
    public void ManySourcesConstructGraph() =>
        ConstructAndDispose(graphConstructionObserver, graphConstructionSlices);

    [Benchmark]
    public void ManySourcesWatchedRaiseDefault()
    {
        for (var pass = 0; pass < raisePasses; ++pass)
            for (var i = 0; i < sourceCount; ++i)
                aDefaultSlices[i].End = pass + 1;
    }

    [Benchmark]
    public void ManySourcesWatchedRaiseGraph()
    {
        for (var pass = 0; pass < raisePasses; ++pass)
            for (var i = 0; i < sourceCount; ++i)
                graphSlices[i].End = pass + 1;
    }

    [Benchmark]
    public void ManySourcesUnwatchedRaiseDefault()
    {
        for (var pass = 0; pass < raisePasses; ++pass)
            for (var i = 0; i < sourceCount; ++i)
                aDefaultSlices[i].Idle = pass + 1;
    }

    [Benchmark]
    public void ManySourcesUnwatchedRaiseGraph()
    {
        for (var pass = 0; pass < raisePasses; ++pass)
            for (var i = 0; i < sourceCount; ++i)
                graphSlices[i].Idle = pass + 1;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < aDefaultObservations.Length; ++i)
        {
            aDefaultObservations[i].Dispose();
            graphObservations[i].Dispose();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        aDefaultConstructionObserver = Observer(true);
        graphConstructionObserver = Observer(false);
        aDefaultObserver = Observer(true);
        graphObserver = Observer(false);
        aDefaultConstructionSlices = new BenchmarkSlice[sourceCount];
        graphConstructionSlices = new BenchmarkSlice[sourceCount];
        aDefaultSlices = new BenchmarkSlice[sourceCount];
        graphSlices = new BenchmarkSlice[sourceCount];
        for (var i = 0; i < sourceCount; ++i)
        {
            aDefaultConstructionSlices[i] = new BenchmarkSlice();
            graphConstructionSlices[i] = new BenchmarkSlice();
            aDefaultSlices[i] = new BenchmarkSlice();
            graphSlices[i] = new BenchmarkSlice();
        }
        aDefaultObservations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * columnCount];
        graphObservations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * columnCount];
        for (var i = 0; i < sourceCount; ++i)
            for (var column = 0; column < columnCount; ++column)
            {
                aDefaultObservations[i * columnCount + column] = aDefaultObserver.Observe(columns[column], aDefaultSlices[i]);
                graphObservations[i * columnCount + column] = graphObserver.Observe(columns[column], graphSlices[i]);
            }
    }
}
