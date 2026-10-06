namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Renders components without a display, counting the batches it would have sent to one
/// </summary>
sealed class BlazorBenchmarkRenderer(IServiceProvider services) :
    Renderer(services, NullLoggerFactory.Instance)
{
    public int Batches { get; private set; }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override void HandleException(Exception exception) =>
        throw new InvalidOperationException("A component threw while being rendered", exception);

    public TComponent Render<TComponent>(ParameterView parameters)
        where TComponent : IComponent =>
        Dispatcher.InvokeAsync(async () =>
        {
            var component = (TComponent)InstantiateComponent(typeof(TComponent));
            await RenderRootComponentAsync(AssignRootComponentId(component), parameters);
            return component;
        }).GetAwaiter().GetResult();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        ++Batches;
        return Task.CompletedTask;
    }
}
