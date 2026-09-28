namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionCountQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery) :
    ObservableCollectionScalarQuery<TElement, int>(collectionObserver, observableCollectionQuery),
    IObservableQueryDependent
{
    ObservableQuerySubscription? observableCollectionQuerySubscription;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
                observableCollectionQuery.UnsubscribeDependent(observableCollectionQuerySubscription!);
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        if (observableCollectionQuery.OperationFault is { } fault)
            Evaluation = (fault, default);
        else
            Evaluation = (null, observableCollectionQuery.Count);
    }

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        Evaluate();
    }

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(observableCollectionQuery);
        observableCollectionQuerySubscription = observableCollectionQuery.SubscribeDependent(this);
        Evaluate();
    }

    public override string ToString() =>
        $"count of {observableCollectionQuery}";
}