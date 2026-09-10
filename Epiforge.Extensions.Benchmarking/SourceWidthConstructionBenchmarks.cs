namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices the construction and disposal of a grid's observations at several row widths, against its own live set and nothing else
/// </summary>
/// <remarks>
/// <see cref="SourceCountBenchmarks" /> measured this until 10 September and measured it wrongly. That class retains two sets of observations for the whole run so that its raise arms have something to raise against, and a construct arm allocating on top of that live set reports the garbage collector: at twelve columns its time per observation rose tenfold while its allocation per observation fell, and collections reached the second generation. Every construction figure taken there is contaminated by an amount nobody measured, the 1.117 ms on record for six columns included
/// </remarks>
/// <remarks>
/// This class retains no observations. It holds only the slices, constructs a grid's worth of observations, disposes them, and does nothing else, so what its arms report is construction and disposal rather than the cost of collecting around a resident population
/// </remarks>
[MemoryDiagnoser]
public class SourceWidthConstructionBenchmarks
{
    const int sourceCount = 1000;

    /// <remarks>
    /// The same expressions in the same order as <see cref="SourceCountBenchmarks" />, so that the two instruments describe one shape at one width
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

    void ConstructAndDispose(ExpressionObserver observer)
    {
        var observations = new IObservableExpression<BenchmarkSlice, bool>[sourceCount * Columns];
        for (var i = 0; i < sourceCount; ++i)
            for (var column = 0; column < Columns; ++column)
                observations[i * Columns + column] = observer.Observe(columns[column], slices[i]);
        for (var i = 0; i < observations.Length; ++i)
            observations[i].Dispose();
    }

    ExpressionObserver aDefaultObserver = null!;
    ExpressionObserver graphObserver = null!;
    BenchmarkSlice[] slices = null!;

    [Params(3, 6, 12)]
    public int Columns { get; set; }

    [Benchmark(Baseline = true)]
    public void ConstructDefault() =>
        ConstructAndDispose(aDefaultObserver);

    [Benchmark]
    public void ConstructGraph() =>
        ConstructAndDispose(graphObserver);

    [GlobalSetup]
    public void Setup()
    {
        aDefaultObserver = new ExpressionObserver();
        graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        slices = new BenchmarkSlice[sourceCount];
        for (var i = 0; i < sourceCount; ++i)
            slices[i] = new BenchmarkSlice();
    }
}
