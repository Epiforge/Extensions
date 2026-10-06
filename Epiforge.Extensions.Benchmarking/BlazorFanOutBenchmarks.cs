namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices changing the value bound to every one of a thousand table cells in one dispatcher work item, each change announcing two properties a cell observes
/// </summary>
/// <remarks>
/// The plain arm changes the same values, which nothing observes, and re-renders the table once, which is what rendering each cell once and no more, in one batch, would cost; the reactive arms are told of every change and decide for themselves how often to render and in how many batches. A batch is what a renderer sends to its display, which on a web view or a server circuit is a message across a boundary this renderer does not have, so the batches each arm sends are checked in setup and recorded with its figures
/// </remarks>
[MemoryDiagnoser]
public class BlazorFanOutBenchmarks
{
    static void AssertDriven(Action operation, Func<int> renders, int expectedRenders, BlazorBenchmarkRenderer renderer, int expectedBatches)
    {
        var rendersBefore = renders();
        var batchesBefore = renderer.Batches;
        operation();
        var rendered = renders() - rendersBefore;
        if (rendered != expectedRenders)
            throw new InvalidOperationException($"An operation rendered cells {rendered} times rather than {expectedRenders}");
        var batches = renderer.Batches - batchesBefore;
        if (batches != expectedBatches)
            throw new InvalidOperationException($"An operation sent {batches} batches rather than {expectedBatches}");
    }

    static void ChangeEveryValue(BlazorBenchmarkBinder[][] binders)
    {
        foreach (var row in binders)
            foreach (var binder in row)
                ++binder.Value;
    }

    BlazorBenchmarkTable<BlazorBenchmarkEpiforgeCell> epiforge = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPhorkCell> phork = null!;
    BlazorBenchmarkTable<BlazorBenchmarkPlainCell> plain = null!;

    [Benchmark]
    public void Epiforge()
    {
        epiforge.Renderer.Dispatcher.InvokeAsync(() => ChangeEveryValue(epiforge.Binders)).GetAwaiter().GetResult();
        epiforge.Settle();
    }

    [Benchmark]
    public void Phork()
    {
        phork.Renderer.Dispatcher.InvokeAsync(() => ChangeEveryValue(phork.Binders)).GetAwaiter().GetResult();
        phork.Settle();
    }

    [Benchmark(Baseline = true)]
    public void Plain() =>
        plain.Renderer.Dispatcher.InvokeAsync(() =>
        {
            ChangeEveryValue(plain.Binders);
            plain.Grid.Refresh();
        }).GetAwaiter().GetResult();

    [GlobalSetup]
    public void Setup()
    {
        const int cells = BlazorBenchmarkTable<IComponent>.Rows * BlazorBenchmarkTable<IComponent>.Columns;
        epiforge = new(true);
        phork = new(true);
        plain = new(true);
        AssertDriven(Epiforge, () => BlazorBenchmarkEpiforgeCell.Renders, cells, epiforge.Renderer, 1);
        AssertDriven(Phork, () => BlazorBenchmarkPhorkCell.Renders, 2 * cells, phork.Renderer, 2 * cells);
        AssertDriven(Plain, () => BlazorBenchmarkPlainCell.Renders, cells, plain.Renderer, 1);
    }
}
