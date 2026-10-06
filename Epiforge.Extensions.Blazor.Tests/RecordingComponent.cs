namespace Epiforge.Extensions.Blazor.Tests;

public sealed class RecordingComponent :
    IReactiveComponent
{
    int configureBindingsCalls;
    int stateHasChangedCalls;

    public Action? ConfiguringBindings { get; set; }

    public int ConfigureBindingsCalls =>
        Volatile.Read(ref configureBindingsCalls);

    public int StateHasChangedCalls =>
        Volatile.Read(ref stateHasChangedCalls);

    public void Attach(RenderHandle renderHandle)
    {
    }

    public void ConfigureBindings()
    {
        Interlocked.Increment(ref configureBindingsCalls);
        ConfiguringBindings?.Invoke();
    }

    public Task SetParametersAsync(ParameterView parameters) =>
        Task.CompletedTask;

    public void StateHasChanged() =>
        Interlocked.Increment(ref stateHasChangedCalls);
}
