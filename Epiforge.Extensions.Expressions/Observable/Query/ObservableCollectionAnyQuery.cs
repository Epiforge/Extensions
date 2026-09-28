namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAnyQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, bool>>? predicate = null) :
    ObservableCollectionScalarQuery<TElement, bool>(collectionObserver, observableCollectionQuery)
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TElement>? where;
    int observableCollectionQueryCount;

    internal readonly Expression<Func<TElement, bool>>? Predicate = predicate;

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
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where is null ? observableCollectionQuery.OperationFault is { } observableCollectionQueryFault ? (observableCollectionQueryFault, default) : (null, observableCollectionQueryCount > 0) : where.OperationFault is { } whereFault ? (whereFault, default) : (null, where.Count > 0);

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        observableCollectionQueryCount = e.Action is NotifyCollectionChangedAction.Reset ? observableCollectionQuery.Count : observableCollectionQueryCount + (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        Evaluate();
    }

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        if (Predicate is not null)
            where = observableCollectionQuery.ObserveWhere(Predicate);
        using var changeHold = HoldChangesOf(where is null ? observableCollectionQuery : Unscoped(where));
        if (Predicate is null)
        {
            observableCollectionQueryCount = observableCollectionQuery.Count;
            observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
            observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        }
        else
        {
            where!.CollectionChanged += WhereCollectionChanged;
            where.PropertyChanged += WherePropertyChanged;
        }
        Evaluate();
    }

    public override string ToString() =>
        Predicate is null ? $"any of {observableCollectionQuery}" : $"any of {observableCollectionQuery} matching {Predicate}";

    void WhereCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        Evaluate();
    }

    void WherePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }
}
