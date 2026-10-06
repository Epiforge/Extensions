namespace Epiforge.Extensions.Blazor.Tests;

sealed class TestRenderer() :
    Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
{
    int batches;
    readonly List<Exception> exceptions = [];

    public int Batches =>
        Volatile.Read(ref batches);

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public IReadOnlyList<Exception> Exceptions
    {
        get
        {
            lock (exceptions)
                return [.. exceptions];
        }
    }

    protected override void HandleException(Exception exception)
    {
        lock (exceptions)
            exceptions.Add(exception);
    }

    public Task<(TComponent component, int componentId)> RenderAsync<TComponent>(ParameterView parameters)
        where TComponent : IComponent =>
        Dispatcher.InvokeAsync(async () =>
        {
            var component = (TComponent)InstantiateComponent(typeof(TComponent));
            var componentId = AssignRootComponentId(component);
            await RenderRootComponentAsync(componentId, parameters);
            return (component, componentId);
        });

    public Task SetParametersAsync(int componentId, ParameterView parameters) =>
        Dispatcher.InvokeAsync(() => RenderRootComponentAsync(componentId, parameters));

    public Task SettleAsync() =>
        Dispatcher.InvokeAsync(() => { });

    /// <summary>
    /// Waits for a condition the dispatcher's work brings about, then lets anything the dispatcher still had queued finish, so that a test can assert that nothing more happened
    /// </summary>
    public async Task SettleUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500 && !condition(); ++attempt)
        {
            await Task.Delay(10);
            await SettleAsync();
        }
        await Task.Delay(50);
        await SettleAsync();
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        Interlocked.Increment(ref batches);
        return Task.CompletedTask;
    }
}
