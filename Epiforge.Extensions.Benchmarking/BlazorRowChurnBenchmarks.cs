namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a hundred rows of ten table cells entering a table and leaving it again, so that every cell is a component instance observing for the first time and then releasing what it observed
/// </summary>
/// <remarks>
/// This is the path a consumer's table takes when a parameter adds or removes its rows. The plain arm reads the same three values without observing anything and is the floor Blazor itself sets for creating, rendering and disposing the cells. The trees arm also builds the three expression trees the reactive cells pass, so the difference between it and a reactive arm is what that library spends
/// </remarks>
[MemoryDiagnoser]
public class BlazorRowChurnBenchmarks
{
    static void AssertDriven(Action operation, Func<int> renders)
    {
        var before = renders();
        operation();
        var rendered = renders() - before;
        if (rendered != BlazorBenchmarkTable<IComponent>.Rows * BlazorBenchmarkTable<IComponent>.Columns)
            throw new InvalidOperationException($"An operation rendered {rendered} new cells");
    }

    BlazorBenchmarkTable<BlazorBenchmarkEpiforgeCell> epiforge = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPhorkCell> phork = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPlainCell> plain = null!;
    BlazorBenchmarkTable<BlazorBenchmarkTreesCell> trees = null!;

    [Benchmark]
    public void Epiforge()
    {
        epiforge.ShowAllRows();
        epiforge.ShowNoRows();
    }

    [Benchmark]
    public void Phork()
    {
        phork.ShowAllRows();
        phork.ShowNoRows();
    }

    [Benchmark(Baseline = true)]
    public void Plain()
    {
        plain.ShowAllRows();
        plain.ShowNoRows();
    }

    [Benchmark]
    public void Trees()
    {
        trees.ShowAllRows();
        trees.ShowNoRows();
    }

    [GlobalSetup]
    public void Setup()
    {
        epiforge = new(false);
        phork = new(false);
        plain = new(false);
        trees = new(false);
        AssertDriven(Epiforge, () => BlazorBenchmarkEpiforgeCell.Renders);
        AssertDriven(Phork, () => BlazorBenchmarkPhorkCell.Renders);
        AssertDriven(Plain, () => BlazorBenchmarkPlainCell.Renders);
        AssertDriven(Trees, () => BlazorBenchmarkTreesCell.Renders);
    }
}
