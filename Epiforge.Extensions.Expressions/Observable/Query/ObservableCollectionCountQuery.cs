namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionCountQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery) :
    ObservableCollectionScalarQuery<TElement, int>(collectionObserver, observableCollectionQuery)
{
    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
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

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"count of {observableCollectionQuery}";
}