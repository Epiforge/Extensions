namespace Epiforge.Extensions.Expressions.Observable.Query;

class ObservableCollectionAllQuery<TElement> :
    ObservableCollectionScalarQuery<TElement, bool>
{
    public ObservableCollectionAllQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, bool>> predicate) :
        base(collectionObserver, observableCollectionQuery) =>
        Predicate = predicate;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TElement>? where;
    int observableCollectionQueryCount;

    internal readonly Expression<Func<TElement, bool>> Predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
                where!.CollectionChanged -= WhereCollectionChanged;
                where.Dispose();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where!.OperationFault is { } whereFault ? (whereFault, default) : (null, observableCollectionQueryCount == where.Count);

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        observableCollectionQueryCount = e.Action is NotifyCollectionChangedAction.Reset ? observableCollectionQuery.Count : observableCollectionQueryCount + (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        Evaluate();
    }

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableCollectionQuery<TElement>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableCollectionQueryCount = observableCollectionQuery.Count;
        where = observableCollectionQuery.ObserveWhere(Predicate);
        where.CollectionChanged += WhereCollectionChanged;
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        Evaluate();
    }

    void WhereCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();
}
