namespace Epiforge.Extensions.Blazor.Tests;

public sealed class LifecycleScript
{
    public Func<Task>? AfterRenderAsync { get; set; }

    public Func<Task>? InitializedAsync { get; set; }

    public Action? Initializing { get; set; }

    public List<string> Log { get; } = [];

    public Func<Task>? ParametersSetAsync { get; set; }

    public bool RenderThrows { get; set; }

    public bool ShouldRender { get; set; } = true;
}
