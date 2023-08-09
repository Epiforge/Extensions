namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAnyQuery<TElement> :
    ObservableCollectionScalarQuery<TElement, bool>
{
    public ObservableCollectionAnyQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, bool>>? predicate = null) :
        base(collectionObserver, observableCollectionQuery) =>
        Predicate = predicate;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TElement>? where;
    int observableCollectionQueryCount;

    internal readonly Expression<Func<TElement, bool>>? Predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                if (where is not null)
                {
                    where.CollectionChanged -= WhereCollectionChanged;
                    where.PropertyChanged -= WherePropertyChanged;
                    where.Dispose();
                }
                else
                {
                    observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                    observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
                }
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where?.OperationFault is { } whereFault ? (whereFault, default) : where is null ? (null, observableCollectionQueryCount > 0) : observableCollectionQuery.OperationFault is { } observableCollectionQueryFault ? (observableCollectionQueryFault, default) : (null, where.Count > 0);

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
        if (Predicate is null)
        {
            observableCollectionQueryCount = observableCollectionQuery.Count;
            observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
            observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        }
        else
        {
            where = observableCollectionQuery.ObserveWhere(Predicate);
            where.CollectionChanged += WhereCollectionChanged;
            where.PropertyChanged += WherePropertyChanged;
        }
        Evaluate();
    }

    void WhereCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();

    void WherePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<TElement>.OperationFault))
            Evaluate();
    }
}
