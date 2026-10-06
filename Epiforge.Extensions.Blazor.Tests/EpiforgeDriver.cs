namespace Epiforge.Extensions.Blazor.Tests;

sealed class EpiforgeDriver :
    IAgreementDriver
{
    public EpiforgeDriver() =>
        (manager, component) = Managers.CreateInitialized();

    readonly RecordingComponent component;
    readonly IReactivityManager manager;

    public int Notifications =>
        component.StateHasChangedCalls;

    public void EndCycle() =>
        manager.NotifyCycleEnded();

    public T Observed<T>(Expression<Func<T>> valueAccessor) =>
        manager.Observed(valueAccessor);

    public T ObservedCollection<T>(Expression<Func<T>> valueAccessor) =>
        manager.ObservedCollection(valueAccessor);
}
