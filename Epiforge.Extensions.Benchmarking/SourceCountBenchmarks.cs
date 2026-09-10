namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices raises in the shape an application laying out a grid has, which is many source objects carrying a few attachments each
/// </summary>
/// <remarks>
/// <see cref="NotificationFanOutBenchmarks" /> measures the opposite shape, one object carrying a thousand attachments, and every conclusion about how a source finds the attachments which want a reported name had been drawn from it alone. These arms were added so that a name index justified by that instrument had somewhere to be disproved, and they disproved it: in this shape the fast path wins every arm, and the graph's advantage on the fan-out instrument was node sharing rather than a better way of finding attachments
/// </remarks>
/// <remarks>
/// One observation of each source per column, each watching a different property of it, which is a row. One raise is of the property one column watches and the rest do not; the other is of a property none of them watches. The subject passes pre-allocated arguments, so a raise allocates nothing of its own
/// </remarks>
/// <remarks>
/// Construction was measured here until 10 September and is now measured by <see cref="SourceWidthConstructionBenchmarks" />, because these arms retain two sets of observations for the whole run and a construct arm allocating on top of that live set measured the garbage collector rather than construction: at twelve columns its time rose tenfold while its allocation per observation fell, and collections reached the second generation
/// </remarks>
[MemoryDiagnoser]
public class SourceCountBenchmarks
{
    const int raisePasses = 100;
    const int sourceCount = 1000;

    /// <remarks>
    /// The first three include <see cref="BenchmarkSlice.End" />, which every arm raises, so that the narrowest width still has a watched column. The first six are the set this instrument carried when its figures were first recorded, which was expected to make width six reproduce them and does not: widening <see cref="BenchmarkSlice" /> to carry twelve columns made every slice larger, so the arms came back within 3.8% rather than identical. Holding the expressions identical does not hold the subject identical
    /// </remarks>
    static readonly Expression<Func<BenchmarkSlice, bool>>[] columns =
    [
        slice => slice.Amount > 0,
        slice => slice.Duration > 0,
        slice => slice.End > 0,
        slice => slice.Rate > 0,
        slice => slice.Start > 0,
        slice => slice.Weight > 0,
        slice => slice.Billable > 0,
        slice => slice.Cost > 0,
        slice => slice.Margin > 0,
        slice => slice.Offset > 0,
        slice => slice.Tax > 0,
        slice => slice.Total > 0
    ];

    static ExpressionObserver Observer(bool useDirectSubscription) =>
        useDirectSubscription ? new ExpressionObserver() : new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });

    IObservableExpression<BenchmarkSlice, bool>[] aDefaultObservations = null!;
    ExpressionObserver aDefaultObserver = null!;
    BenchmarkSlice[] aDefaultSlices = null!;
    IObservableExpression<BenchmarkSlice, bool>[] graphObservations = null!;
    ExpressionObserver graphObserver = null!;
    BenchmarkSlice[] graphSlices = null!;

    /// <remarks>
    /// How many of <see cref="columns" /> each slice is observed through, which is the width of a row. This was added to confirm that each mechanism charges a constant amount per attachment on a source, and it refuted that: both charge more per attachment as the row widens, the graph by 2.17x across this range against the fast path's 1.59x, so the advantage grows with width instead of holding. What the widths vary is therefore not the slope of a line but a working set
    /// </remarks>
    [Params(3, 6, 12)]
    public int Columns { get; set; }

    [Benchmark(Baseline = true)]
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
        aDefaultObserver = Observer(true);
        graphObserver = Observer(false);
        aDefaultSlices = new BenchmarkSlice[sourceCount];
        graphSlices = new BenchmarkSlice[sourceCount];
        for (var i = 0; i < sourceCount; ++i)
        {
            aDefaultSlices[i] = new BenchmarkSlice();
            graphSlices[i] = new BenchmarkSlice();
        }
        aDefaultObservations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * Columns];
        graphObservations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * Columns];
        for (var i = 0; i < sourceCount; ++i)
            for (var column = 0; column < Columns; ++column)
            {
                aDefaultObservations[i * Columns + column] = aDefaultObserver.Observe(columns[column], aDefaultSlices[i]);
                graphObservations[i * Columns + column] = graphObserver.Observe(columns[column], graphSlices[i]);
            }
    }
}
