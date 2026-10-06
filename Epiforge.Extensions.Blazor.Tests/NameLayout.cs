namespace Epiforge.Extensions.Blazor.Tests;

public sealed class NameLayout :
    ReactiveLayoutComponentBase
{
    public int Renders { get; private set; }

    [Parameter]
    public Person? Person { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ++Renders;
        builder.AddContent(0, ReactivityManager.Observed(() => Person!.Name));
        builder.AddContent(1, Body);
    }
}
