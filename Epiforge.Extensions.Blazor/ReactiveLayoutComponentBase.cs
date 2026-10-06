namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Provides the base class for layouts which re-render when what they observe changes
/// </summary>
public abstract class ReactiveLayoutComponentBase :
    ReactiveComponentBase
{
    /// <summary>
    /// Gets/sets the content to be rendered inside the layout
    /// </summary>
    [Parameter]
    public RenderFragment? Body { get; set; }

    /// <inheritdoc/>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReactiveLayoutComponentBase))]
    public override Task SetParametersAsync(ParameterView parameters) =>
        base.SetParametersAsync(parameters);
}
