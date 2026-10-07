namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices constructing and disposing a thousand observations of an invocation the fast path cannot reduce to its body, because its parameter is read eight times and some of those reads are in conditional branches
/// </summary>
/// <remarks>
/// The default arm landing on the graph arm is the proof that the fast path refused the shape; once it admits the invocation as a let, the default arm is what that is worth. The hand-reduced arm observes the same body with the invocation written out, which the fast path already serves, so it prices the body itself on the fast path, reading the property once for each read of the parameter. Nothing is retained between operations, so that construction is measured against an empty heap rather than a resident population
/// </remarks>
[MemoryDiagnoser]
public class InvocationLetConstructionBenchmarks
{
    const int observationCount = 1000;

    ExpressionObserver aDefault = null!;
    BenchmarkDuration[] durations = null!;
    ExpressionObserver graph = null!;

    static void ConstructAndDispose(ExpressionObserver observer, Expression<Func<BenchmarkDuration, string>> lambda, BenchmarkDuration[] durations)
    {
        for (var i = 0; i < durations.Length; ++i)
            observer.Observe(lambda, durations[i]).Dispose();
    }

    [Benchmark]
    public void HandReducedDefault() =>
        ConstructAndDispose(aDefault, InvocationLetShapes.HandReduced, durations);

    [Benchmark]
    public void LetDefault() =>
        ConstructAndDispose(aDefault, InvocationLetShapes.Let, durations);

    [Benchmark(Baseline = true)]
    public void LetGraph() =>
        ConstructAndDispose(graph, InvocationLetShapes.Let, durations);

    [GlobalSetup]
    public void Setup()
    {
        aDefault = new ExpressionObserver();
        graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        durations = new BenchmarkDuration[observationCount];
        for (var i = 0; i < observationCount; ++i)
            durations[i] = new(TimeSpan.FromSeconds(i * 37 - 5000));
    }
}
