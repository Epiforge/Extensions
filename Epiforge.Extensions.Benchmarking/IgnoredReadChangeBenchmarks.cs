namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices one change to each of a thousand standing observations of a property whose change notifications are ignored, read through something which can move
/// </summary>
/// <remarks>
/// Each arm holds its own thousand observations over its own holders, so that no arm's raises reach another's observations. A change to the followed property re-evaluates every arm; a change to the ignored property reaches only the arm which follows it; a move of what it is read through makes every arm read it again. The ignoring default arm landing on the graph arm is the proof that the fast path refused the shape, and the following default arm is what a caller who had not asked to save anything pays
/// </remarks>
[MemoryDiagnoser]
public class IgnoredReadChangeBenchmarks
{
    public enum Change
    {
        Followed,
        Ignored,
        Moved
    }

    const int observationCount = 1000;

    (BenchmarkHolder[] Holders, BenchmarkHolding[] Others) followingDefault;
    (BenchmarkHolder[] Holders, BenchmarkHolding[] Others) ignoringDefault;
    (BenchmarkHolder[] Holders, BenchmarkHolding[] Others) ignoringGraph;
    readonly List<IDisposable> observations = [];

    [Params(Change.Followed, Change.Ignored, Change.Moved)]
    public Change Made { get; set; }

    void Make((BenchmarkHolder[] Holders, BenchmarkHolding[] Others) observed)
    {
        var (holders, others) = observed;
        switch (Made)
        {
            case Change.Followed:
                for (var i = 0; i < holders.Length; ++i)
                    holders[i].Score ^= 1;
                break;
            case Change.Ignored:
                for (var i = 0; i < holders.Length; ++i)
                    holders[i].Holding.Rank ^= 1;
                break;
            case Change.Moved:
                for (var i = 0; i < holders.Length; ++i)
                {
                    var holder = holders[i];
                    var other = others[i];
                    others[i] = holder.Holding;
                    holder.Holding = other;
                }
                break;
        }
    }

    (BenchmarkHolder[] Holders, BenchmarkHolding[] Others) Observed(ExpressionObserver observer)
    {
        var holders = new BenchmarkHolder[observationCount];
        var others = new BenchmarkHolding[observationCount];
        for (var i = 0; i < observationCount; ++i)
        {
            holders[i] = new(new(i));
            others[i] = new(-i);
            observations.Add(observer.Observe(IgnoredReadShapes.Reading, holders[i]));
        }
        return (holders, others);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var observation in observations)
            observation.Dispose();
        observations.Clear();
    }

    [Benchmark]
    public void FollowingDefault() =>
        Make(followingDefault);

    [Benchmark]
    public void IgnoringDefault() =>
        Make(ignoringDefault);

    [Benchmark(Baseline = true)]
    public void IgnoringGraph() =>
        Make(ignoringGraph);

    [GlobalSetup]
    public void Setup()
    {
        followingDefault = Observed(IgnoredReadShapes.FollowingDefault());
        ignoringDefault = Observed(IgnoredReadShapes.IgnoringDefault());
        ignoringGraph = Observed(IgnoredReadShapes.IgnoringGraph());
    }
}
