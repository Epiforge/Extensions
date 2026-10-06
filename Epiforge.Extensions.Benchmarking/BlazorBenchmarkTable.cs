namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A renderer, the data of a table and a grid of one cell type rendered over it
/// </summary>
sealed class BlazorBenchmarkTable<TCell>
    where TCell : IComponent
{
    public const int Columns = 10;
    public const int Rows = 100;

    static readonly IServiceProvider services = Phork.Blazor.ServiceCollectionExtensions.AddPhorkBlazorReactivity(new ServiceCollection()).BuildServiceProvider();

    public BlazorBenchmarkTable(bool showAllRows)
    {
        Binders = new BlazorBenchmarkBinder[Rows][];
        for (var row = 0; row < Rows; ++row)
        {
            Binders[row] = new BlazorBenchmarkBinder[Columns];
            for (var column = 0; column < Columns; ++column)
                Binders[row][column] = new();
        }
        columns = new BlazorBenchmarkColumn[Columns];
        for (var column = 0; column < Columns; ++column)
            columns[column] = new();
        Renderer = new(services);
        noRows = ParametersFor(0);
        allRows = ParametersFor(Rows);
        Grid = Renderer.Render<BlazorBenchmarkGrid<TCell>>(showAllRows ? allRows : noRows);
    }

    readonly ParameterView allRows;
    readonly BlazorBenchmarkColumn[] columns;
    readonly ParameterView noRows;

    public BlazorBenchmarkBinder[][] Binders { get; }

    public BlazorBenchmarkGrid<TCell> Grid { get; }

    public BlazorBenchmarkRenderer Renderer { get; }

    ParameterView ParametersFor(int rows) =>
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BlazorBenchmarkGrid<TCell>.Binders)] = Binders,
            [nameof(BlazorBenchmarkGrid<TCell>.Columns)] = columns,
            [nameof(BlazorBenchmarkGrid<TCell>.Rows)] = rows
        });

    public void Refresh() =>
        Renderer.Dispatcher.InvokeAsync(Grid.Refresh).GetAwaiter().GetResult();

    public void ShowAllRows() =>
        Renderer.Dispatcher.InvokeAsync(() => Grid.SetParametersAsync(allRows)).GetAwaiter().GetResult();

    public void ShowNoRows() =>
        Renderer.Dispatcher.InvokeAsync(() => Grid.SetParametersAsync(noRows)).GetAwaiter().GetResult();

    public void Settle() =>
        Renderer.Dispatcher.InvokeAsync(() => { }).GetAwaiter().GetResult();
}
