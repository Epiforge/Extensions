namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a change to the argument of a thousand standing observations of an invocation the fast path cannot reduce to its body, one change to each observation's own duration
/// </summary>
/// <remarks>
/// Each arm holds its own thousand observations over its own durations, so that no arm's raises reach another's observations. The default arm landing on the graph arm is the proof that the fast path refused the shape. The hand-reduced arm re-evaluates the same body with the invocation written out, on the fast path, reading the property once for each read of the parameter
/// </remarks>
[MemoryDiagnoser]
public class InvocationLetChangeBenchmarks
{
    const int observationCount = 1000;

    BenchmarkDuration[] handReducedDefault = null!;
    BenchmarkDuration[] letDefault = null!;
    BenchmarkDuration[] letGraph = null!;
    readonly List<IDisposable> observations = [];
    TimeSpan step;

    static void Advance(BenchmarkDuration[] durations, TimeSpan step)
    {
        for (var i = 0; i < durations.Length; ++i)
            durations[i].Elapsed += step;
    }

    BenchmarkDuration[] Observed(ExpressionObserver observer, Expression<Func<BenchmarkDuration, string>> lambda)
    {
        var durations = new BenchmarkDuration[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            durations[i] = new(TimeSpan.FromSeconds(i * 37 - 5000));
            observations.Add(observer.Observe(lambda, durations[i]));
        }
        return durations;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var observation in observations)
            observation.Dispose();
        observations.Clear();
    }

    [Benchmark]
    public void HandReducedDefault() =>
        Advance(handReducedDefault, step = -step);

    [Benchmark]
    public void LetDefault() =>
        Advance(letDefault, step = -step);

    [Benchmark(Baseline = true)]
    public void LetGraph() =>
        Advance(letGraph, step = -step);

    [GlobalSetup]
    public void Setup()
    {
        var aDefault = new ExpressionObserver();
        var graph = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        step = TimeSpan.FromSeconds(61);
        handReducedDefault = Observed(aDefault, InvocationLetShapes.HandReduced);
        letDefault = Observed(aDefault, InvocationLetShapes.Let);
        letGraph = Observed(graph, InvocationLetShapes.Let);
    }
}
