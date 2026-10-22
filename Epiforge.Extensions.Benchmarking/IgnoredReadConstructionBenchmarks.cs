namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices constructing and disposing a thousand observations of a property whose change notifications are ignored, read through something which can move
/// </summary>
/// <remarks>
/// The ignoring default arm landing on the graph arm is the proof that the fast path refused the shape; once it hosts the read of the ignored property alone, the ignoring default arm is what that is worth, and the following default arm is the floor the fast path sets for the same shape when nothing is ignored. Nothing is retained between operations, so that construction is measured against an empty heap rather than a resident population
/// </remarks>
[MemoryDiagnoser]
public class IgnoredReadConstructionBenchmarks
{
    const int observationCount = 1000;

    ExpressionObserver followingDefault = null!;
    BenchmarkHolder[] holders = null!;
    ExpressionObserver ignoringDefault = null!;
    ExpressionObserver ignoringGraph = null!;

    static void ConstructAndDispose(ExpressionObserver observer, BenchmarkHolder[] holders)
    {
        for (var i = 0; i < holders.Length; ++i)
            observer.Observe(IgnoredReadShapes.Reading, holders[i]).Dispose();
    }

    [Benchmark]
    public void FollowingDefault() =>
        ConstructAndDispose(followingDefault, holders);

    [Benchmark]
    public void IgnoringDefault() =>
        ConstructAndDispose(ignoringDefault, holders);

    [Benchmark(Baseline = true)]
    public void IgnoringGraph() =>
        ConstructAndDispose(ignoringGraph, holders);

    [GlobalSetup]
    public void Setup()
    {
        followingDefault = IgnoredReadShapes.FollowingDefault();
        ignoringDefault = IgnoredReadShapes.IgnoringDefault();
        ignoringGraph = IgnoredReadShapes.IgnoringGraph();
        holders = new BenchmarkHolder[observationCount];
        for (var i = 0; i < observationCount; ++i)
            holders[i] = new(new(i));
    }
}
