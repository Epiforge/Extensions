namespace Epiforge.Extensions.Blazor.Tests;

sealed class PhorkDriver :
    IAgreementDriver
{
    sealed class RecordingComponent :
        ComponentBase,
        Phork.Blazor.IReactiveComponent
    {
        int stateHasChangedCalls;

        public int StateHasChangedCalls =>
            Volatile.Read(ref stateHasChangedCalls);

        public void ConfigureBindings()
        {
        }

        void Phork.Blazor.IReactiveComponent.StateHasChanged() =>
            Interlocked.Increment(ref stateHasChangedCalls);
    }

    static readonly IServiceProvider services = Phork.Blazor.ServiceCollectionExtensions.AddPhorkBlazorReactivity(new ServiceCollection()).BuildServiceProvider();

    public PhorkDriver()
    {
        manager = services.GetRequiredService<Phork.Blazor.IReactivityManager>();
        component = new RecordingComponent();
        manager.Initialize(component);
    }

    readonly RecordingComponent component;
    readonly Phork.Blazor.IReactivityManager manager;

    public int Notifications =>
        component.StateHasChangedCalls;

    public void EndCycle() =>
        manager.NotifyCycleEnded();

    public T Observed<T>(Expression<Func<T>> valueAccessor) =>
        manager.Observed(valueAccessor);

    public T ObservedCollection<T>(Expression<Func<T>> valueAccessor) =>
        manager.ObservedCollection(valueAccessor);
}
