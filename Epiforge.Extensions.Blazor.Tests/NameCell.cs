namespace Epiforge.Extensions.Blazor.Tests;

public sealed class NameCell :
    ReactiveComponentBase
{
    int renders;

    [Parameter]
    public bool DeclinesToRerender { get; set; }

    public string? LastName { get; private set; }

    [Parameter]
    public Person? Person { get; set; }

    public bool RenderedOffTheDispatcher { get; private set; }

    public int Renders =>
        Volatile.Read(ref renders);

    [Parameter]
    public bool ShowAge { get; set; }

    protected override bool ShouldRender() =>
        !DeclinesToRerender;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Interlocked.Increment(ref renders);
        if (SynchronizationContext.Current?.GetType().Name.Contains("Renderer", StringComparison.Ordinal) != true)
            RenderedOffTheDispatcher = true;
        if (Person is { } person)
        {
            LastName = Observed(() => person.Name);
            builder.AddContent(0, LastName);
            if (ShowAge)
                builder.AddContent(1, Observed(() => person.Age));
        }
    }
}
