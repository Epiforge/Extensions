namespace Epiforge.Extensions.Blazor.Tests;

public interface ILifecycleTwin :
    IComponent
{
    Task ChangeStateAsync(Dispatcher dispatcher);

    Task HandleEventAsync(Dispatcher dispatcher, Func<Task> handler);
}
