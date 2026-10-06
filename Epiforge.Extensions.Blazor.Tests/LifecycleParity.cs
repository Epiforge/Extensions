namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class LifecycleParity
{
    static ParameterView Parameters(LifecycleScript script, int value) =>
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(StockTwin.Script)] = script, [nameof(StockTwin.Value)] = value });

    static async Task<List<string>> RunAsync<TTwin>(Action<LifecycleScript> configure, Func<TestRenderer, TTwin, int, LifecycleScript, Task> scenario)
        where TTwin : ILifecycleTwin
    {
        using var renderer = new TestRenderer();
        var script = new LifecycleScript();
        configure(script);
        TTwin twin = default!;
        var componentId = 0;
        try
        {
            (twin, componentId) = await renderer.RenderAsync<TTwin>(Parameters(script, 1));
        }
        catch (Exception exception)
        {
            script.Log.Add($"RenderAsync threw {exception.GetType().Name}");
        }
        if (twin is not null)
            try
            {
                await scenario(renderer, twin, componentId, script);
            }
            catch (Exception exception)
            {
                script.Log.Add($"scenario threw {exception.GetType().Name}");
            }
        await renderer.SettleAsync();
        script.Log.Add($"{renderer.Batches} batches");
        foreach (var exception in renderer.Exceptions)
            script.Log.Add($"renderer handled {exception.GetType().Name}: {exception.Message}");
        return script.Log;
    }

    static async Task AssertParityAsync(Action<LifecycleScript> configure, Func<TestRenderer, ILifecycleTwin, int, LifecycleScript, Task> scenario)
    {
        var stock = await RunAsync<StockTwin>(configure, (renderer, twin, componentId, script) => scenario(renderer, twin, componentId, script));
        var reactive = await RunAsync<ReactiveTwin>(configure, (renderer, twin, componentId, script) => scenario(renderer, twin, componentId, script));
        CollectionAssert.AreEqual(stock, reactive, $"ComponentBase:{Environment.NewLine}{string.Join(Environment.NewLine, stock)}{Environment.NewLine}ReactiveComponentBase:{Environment.NewLine}{string.Join(Environment.NewLine, reactive)}");
    }

    static Task NothingMore(TestRenderer renderer, ILifecycleTwin twin, int componentId, LifecycleScript script) =>
        Task.CompletedTask;

    [TestMethod]
    public Task AFaultedParametersSetAsync() =>
        AssertParityAsync(script => script.ParametersSetAsync = () => Task.FromException(new InvalidOperationException("faulted")), NothingMore);

    [TestMethod]
    public Task ACanceledInitializedAsync() =>
        AssertParityAsync(script => script.InitializedAsync = () => Task.FromCanceled(new CancellationToken(true)), NothingMore);

    [TestMethod]
    public Task AFaultedInitializedAsync() =>
        AssertParityAsync(script => script.InitializedAsync = () => Task.FromException(new InvalidOperationException("faulted")), NothingMore);

    [TestMethod]
    public Task AnAsynchronousEventHandler() =>
        AssertParityAsync(_ => { }, async (renderer, twin, componentId, script) =>
        {
            await twin.HandleEventAsync(renderer.Dispatcher, async () =>
            {
                script.Log.Add("handler begins");
                await Task.Yield();
                script.Log.Add("handler ends");
            });
            await renderer.SettleAsync();
        });

    [TestMethod]
    public Task AnAsynchronousInitializedAsync() =>
        AssertParityAsync(script => script.InitializedAsync = async () =>
        {
            await Task.Yield();
            script.Log.Add("initialized");
        }, NothingMore);

    [TestMethod]
    public Task AnAsynchronousParametersSetAsync() =>
        AssertParityAsync(script => script.ParametersSetAsync = async () =>
        {
            await Task.Yield();
            script.Log.Add("parameters set");
        }, NothingMore);

    [TestMethod]
    public Task AnExceptionWhileRendering() =>
        AssertParityAsync(_ => { }, async (renderer, twin, componentId, script) =>
        {
            script.RenderThrows = true;
            await renderer.SetParametersAsync(componentId, Parameters(script, 2));
        });

    [TestMethod]
    public Task AnExceptionInOnInitialized() =>
        AssertParityAsync(script => script.Initializing = () => throw new InvalidOperationException("thrown"), NothingMore);

    [TestMethod]
    public Task ASynchronousEventHandler() =>
        AssertParityAsync(_ => { }, (renderer, twin, componentId, script) => twin.HandleEventAsync(renderer.Dispatcher, () =>
        {
            script.Log.Add("handled");
            return Task.CompletedTask;
        }));

    [TestMethod]
    public Task NewParameters() =>
        AssertParityAsync(_ => { }, async (renderer, twin, componentId, script) =>
        {
            await renderer.SetParametersAsync(componentId, Parameters(script, 2));
            await renderer.SetParametersAsync(componentId, Parameters(script, 3));
        });

    [TestMethod]
    public Task ShouldRenderDeclining() =>
        AssertParityAsync(script => script.ShouldRender = false, async (renderer, twin, componentId, script) =>
        {
            await renderer.SetParametersAsync(componentId, Parameters(script, 2));
            await twin.ChangeStateAsync(renderer.Dispatcher);
            script.ShouldRender = true;
            await twin.ChangeStateAsync(renderer.Dispatcher);
        });

    [TestMethod]
    public Task StateChangesBetweenRenders() =>
        AssertParityAsync(_ => { }, async (renderer, twin, componentId, script) =>
        {
            await twin.ChangeStateAsync(renderer.Dispatcher);
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                await twin.ChangeStateAsync(renderer.Dispatcher);
                await twin.ChangeStateAsync(renderer.Dispatcher);
            });
        });

    [TestMethod]
    public Task TheFirstRender() =>
        AssertParityAsync(_ => { }, NothingMore);
}
