namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Represents a component which re-renders when what it observes changes
/// </summary>
public interface IReactiveComponent :
    IComponent
{
    /// <summary>
    /// Configures bindings at the end of each render cycle
    /// </summary>
    void ConfigureBindings();

    /// <summary>
    /// Notifies the component that its state has changed, which may be done from any thread
    /// </summary>
    void StateHasChanged();
}
