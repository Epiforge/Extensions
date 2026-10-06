namespace Epiforge.Extensions.Blazor.Tests;

public interface IAgreementDriver
{
    int Notifications { get; }

    void EndCycle();

    T Observed<T>(Expression<Func<T>> valueAccessor);

    T ObservedCollection<T>(Expression<Func<T>> valueAccessor);
}
