namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A table of cells of one component type, one per row and column, the shape in which a consumer met the costs these benchmarks measure
/// </summary>
sealed class BlazorBenchmarkGrid<TCell> :
    ComponentBase
    where TCell : IComponent
{
    [Parameter]
    public BlazorBenchmarkBinder[][] Binders { get; set; } = [];

    [Parameter]
    public BlazorBenchmarkColumn[] Columns { get; set; } = [];

    [Parameter]
    public int Rows { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        for (var row = 0; row < Rows; ++row)
        {
            builder.OpenElement(0, "tr");
            builder.SetKey(Binders[row]);
            for (var column = 0; column < Columns.Length; ++column)
            {
                builder.OpenComponent<TCell>(1);
                builder.AddAttribute(2, "Binder", Binders[row][column]);
                builder.AddAttribute(3, "Column", Columns[column]);
                builder.CloseComponent();
            }
            builder.CloseElement();
        }
    }

    public void Refresh() =>
        StateHasChanged();
}
