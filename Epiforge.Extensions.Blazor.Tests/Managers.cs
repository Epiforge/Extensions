namespace Epiforge.Extensions.Blazor.Tests;

static class Managers
{
    static readonly IServiceProvider services = new ServiceCollection().AddEpiforgeBlazorReactivity().BuildServiceProvider();

    public static IReactivityManager Create() =>
        services.GetRequiredService<IReactivityManager>();

    public static (IReactivityManager manager, RecordingComponent component) CreateInitialized()
    {
        var manager = Create();
        var component = new RecordingComponent();
        manager.Initialize(component);
        return (manager, component);
    }
}
