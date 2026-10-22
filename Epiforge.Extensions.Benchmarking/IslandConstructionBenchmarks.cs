namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices constructing and disposing a thousand observations of an expression refused at its root, and of its operand alone on each mechanism, by the width of that operand
/// </summary>
/// <remarks>
/// What hosting the operand as a direct island inside the graph could be worth is estimated from these before it is built: the refused expression on the graph, less its operand on the graph, plus its operand on the fast path, plus whatever hosting costs. The refused default arm landing on the refused graph arm is the proof that the default observer builds the whole graph today; once islands exist, it is what they are worth. Nothing is retained between operations, so that construction is measured against an empty heap rather than a resident population
/// </remarks>
[MemoryDiagnoser]
public class IslandConstructionBenchmarks
{
    const int observationCount = 1000;

    ExpressionObserver aDefault = null!;
    ExpressionObserver graph = null!;
    Expression<Func<BenchmarkWide, int>> operand = null!;
    Expression<Func<BenchmarkWide, BenchmarkLease>> refused = null!;
    BenchmarkWide[] wides = null!;

    [Params(1, 2, 4, 8)]
    public int Width { get; set; }

    static void ConstructAndDispose<TResult>(ExpressionObserver observer, Expression<Func<BenchmarkWide, TResult>> lambda, BenchmarkWide[] wides)
    {
        for (var i = 0; i < wides.Length; ++i)
            observer.Observe(lambda, wides[i]).Dispose();
    }

    [Benchmark]
    public void OperandDefault() =>
        ConstructAndDispose(aDefault, operand, wides);

    [Benchmark]
    public void OperandGraph() =>
        ConstructAndDispose(graph, operand, wides);

    [Benchmark]
    public void RefusedDefault() =>
        ConstructAndDispose(aDefault, refused, wides);

    [Benchmark(Baseline = true)]
    public void RefusedGraph() =>
        ConstructAndDispose(graph, refused, wides);

    [GlobalSetup]
    public void Setup()
    {
        aDefault = new ExpressionObserver();
        graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        operand = IslandShapes.Operand(Width);
        refused = IslandShapes.Refused(Width);
        wides = new BenchmarkWide[observationCount];
        for (var i = 0; i < observationCount; ++i)
            wides[i] = new(i);
    }
}
