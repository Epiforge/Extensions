namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A cell which builds the same three expression trees the reactive cells pass to be observed and then reads the values directly, so that what the C# compiler spends building the trees is priced apart from what observing them costs
/// </summary>
sealed class BlazorBenchmarkTreesCell :
    ComponentBase
{
    public static int Renders;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static T Built<T>(Expression<Func<T>> valueAccessor, T value)
    {
        GC.KeepAlive(valueAccessor);
        return value;
    }

    [Parameter]
    public BlazorBenchmarkBinder? Binder { get; set; }

    [Parameter]
    public BlazorBenchmarkColumn? Column { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ++Renders;
        if (Column is { } column && Binder is { } binder)
        {
            var value = Built(() => binder.Value, binder.Value);
            var textValue = Built(() => binder.TextValue, binder.TextValue);
            var displayDataType = Built(() => column.DisplayDataType, column.DisplayDataType);
            builder.OpenElement(0, "td");
            builder.AddAttribute(1, "class", displayDataType == TypeCode.Int32 && value >= 0 ? "number" : "text");
            builder.AddContent(2, textValue);
            builder.CloseElement();
        }
    }
}
