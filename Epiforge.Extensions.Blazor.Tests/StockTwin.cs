namespace Epiforge.Extensions.Blazor.Tests;

public sealed class StockTwin :
    ComponentBase,
    ILifecycleTwin
{
    [Parameter]
    public LifecycleScript Script { get; set; } = default!;

    [Parameter]
    public int Value { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Script.Log.Add($"Render {Value}");
        if (Script.RenderThrows)
            throw new InvalidOperationException("render");
        builder.AddContent(0, Value);
    }

    public Task ChangeStateAsync(Dispatcher dispatcher) =>
        dispatcher.InvokeAsync(StateHasChanged);

    public Task HandleEventAsync(Dispatcher dispatcher, Func<Task> handler) =>
        dispatcher.InvokeAsync(() => ((IHandleEvent)this).HandleEventAsync(new EventCallbackWorkItem(handler), null));

    protected override void OnAfterRender(bool firstRender) =>
        Script.Log.Add($"OnAfterRender {firstRender}");

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        Script.Log.Add($"OnAfterRenderAsync {firstRender}");
        return Script.AfterRenderAsync?.Invoke() ?? Task.CompletedTask;
    }

    protected override void OnInitialized()
    {
        Script.Log.Add("OnInitialized");
        Script.Initializing?.Invoke();
    }

    protected override Task OnInitializedAsync()
    {
        Script.Log.Add("OnInitializedAsync");
        return Script.InitializedAsync?.Invoke() ?? Task.CompletedTask;
    }

    protected override void OnParametersSet() =>
        Script.Log.Add($"OnParametersSet {Value}");

    protected override Task OnParametersSetAsync()
    {
        Script.Log.Add($"OnParametersSetAsync {Value}");
        return Script.ParametersSetAsync?.Invoke() ?? Task.CompletedTask;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (Script is not null)
            Script.Log.Add("SetParametersAsync");
        return base.SetParametersAsync(parameters);
    }

    protected override bool ShouldRender()
    {
        Script.Log.Add("ShouldRender");
        return Script.ShouldRender;
    }
}
