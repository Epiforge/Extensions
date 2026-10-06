namespace Epiforge.Extensions.Benchmarking;

sealed class BlazorBenchmarkEpiforgeCell :
    ReactiveComponentBase
{
    public static int Renders;

    [Parameter]
    public BlazorBenchmarkBinder? Binder { get; set; }

    [Parameter]
    public BlazorBenchmarkColumn? Column { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ++Renders;
        if (Column is { } column && Binder is { } binder)
        {
            var value = Observed(() => binder.Value);
            var textValue = Observed(() => binder.TextValue);
            var displayDataType = Observed(() => column.DisplayDataType);
            builder.OpenElement(0, "td");
            builder.AddAttribute(1, "class", displayDataType == TypeCode.Int32 && value >= 0 ? "number" : "text");
            builder.AddContent(2, textValue);
            builder.CloseElement();
        }
    }
}
