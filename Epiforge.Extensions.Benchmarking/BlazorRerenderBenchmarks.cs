namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices re-rendering a thousand table cells which observe exactly what they observed the last time they rendered, the path every render after a component's first takes
/// </summary>
/// <remarks>
/// Each cell reads three values, as the consumer's table cells do. The plain arm reads them without observing anything and is the floor Blazor itself sets. The trees arm also builds the three expression trees the reactive cells pass, which the C# compiler does on every call whatever library receives them, so the difference between it and a reactive arm is what that library spends
/// </remarks>
[MemoryDiagnoser]
public class BlazorRerenderBenchmarks
{
    static void AssertDriven(Action operation, Func<int> renders)
    {
        var before = renders();
        operation();
        var rendered = renders() - before;
        if (rendered != BlazorBenchmarkTable<IComponent>.Rows * BlazorBenchmarkTable<IComponent>.Columns)
            throw new InvalidOperationException($"An operation re-rendered {rendered} cells");
    }

    BlazorBenchmarkTable<BlazorBenchmarkEpiforgeCell> epiforge = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPhorkCell> phork = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPlainCell> plain = null!;
    BlazorBenchmarkTable<BlazorBenchmarkTreesCell> trees = null!;

    [Benchmark]
    public void Epiforge() =>
        epiforge.Refresh();

    [Benchmark]
    public void Phork() =>
        phork.Refresh();

    [Benchmark(Baseline = true)]
    public void Plain() =>
        plain.Refresh();

    [Benchmark]
    public void Trees() =>
        trees.Refresh();

    [GlobalSetup]
    public void Setup()
    {
        epiforge = new(true);
        phork = new(true);
        plain = new(true);
        trees = new(true);
        AssertDriven(Epiforge, () => BlazorBenchmarkEpiforgeCell.Renders);
        AssertDriven(Phork, () => BlazorBenchmarkPhorkCell.Renders);
        AssertDriven(Plain, () => BlazorBenchmarkPlainCell.Renders);
        AssertDriven(Trees, () => BlazorBenchmarkTreesCell.Renders);
    }
}
