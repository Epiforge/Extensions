namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Receives the notifications of a query as a query derived from it, before any handler subscribed to that query's events and without a delegate for each of them
/// </summary>
interface IObservableQueryDependent
{
    void OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e);

    void OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
    }

    void OnDependencyPropertyChanging(ObservableQuerySubscription subscription, PropertyChangingEventArgs e)
    {
    }
}
