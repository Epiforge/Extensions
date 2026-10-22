namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices one change to each of a thousand standing observations of an expression refused at its root, and of its operand alone on each mechanism, by the width of that operand
/// </summary>
/// <remarks>
/// Each arm holds its own thousand observations over its own objects, so that no arm's raises reach another's observations. The change is to the first property the operand reads, so every arm re-evaluates and every refused arm constructs a lease and disposes of the one it replaces. The estimate of what an island is worth is made as it is for construction
/// </remarks>
[MemoryDiagnoser]
public class IslandChangeBenchmarks
{
    const int observationCount = 1000;

    BenchmarkWide[] operandDefault = null!;
    BenchmarkWide[] operandGraph = null!;
    readonly List<IDisposable> observations = [];
    BenchmarkWide[] refusedDefault = null!;
    BenchmarkWide[] refusedGraph = null!;
    int step;

    [Params(1, 2, 4, 8)]
    public int Width { get; set; }

    static void Change(BenchmarkWide[] wides, int step)
    {
        for (var i = 0; i < wides.Length; ++i)
            wides[i].V0 = step;
    }

    BenchmarkWide[] Observed<TResult>(ExpressionObserver observer, Expression<Func<BenchmarkWide, TResult>> lambda)
    {
        var wides = new BenchmarkWide[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            wides[i] = new(i);
            observations.Add(observer.Observe(lambda, wides[i]));
        }
        return wides;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var observation in observations)
            observation.Dispose();
        observations.Clear();
    }

    [Benchmark]
    public void OperandDefault() =>
        Change(operandDefault, ++step);

    [Benchmark]
    public void OperandGraph() =>
        Change(operandGraph, ++step);

    [Benchmark]
    public void RefusedDefault() =>
        Change(refusedDefault, ++step);

    [Benchmark(Baseline = true)]
    public void RefusedGraph() =>
        Change(refusedGraph, ++step);

    [GlobalSetup]
    public void Setup()
    {
        var aDefault = new ExpressionObserver();
        var graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        var operand = IslandShapes.Operand(Width);
        var refused = IslandShapes.Refused(Width);
        operandDefault = Observed(aDefault, operand);
        operandGraph = Observed(graph, operand);
        refusedDefault = Observed(aDefault, refused);
        refusedGraph = Observed(graph, refused);
    }
}
