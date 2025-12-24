namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryAnyQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, Expression<Func<TKey, TValue, bool>>? predicate = null) :
    ObservableDictionaryScalarQuery<TKey, TValue, bool>(collectionObserver, observableDictionaryQuery)
    where TKey : notnull
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableDictionaryQuery<TKey, TValue>? where;
    int observableDictionaryQueryCount;

    internal readonly Expression<Func<TKey, TValue, bool>>? Predicate = predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                if (where is not null)
                {
                    ((INotifyDictionaryChanged<TKey, TValue>)where!).DictionaryChanged -= WhereDictionaryChanged;
                    where.PropertyChanged -= WherePropertyChanged;
                    where.Dispose();
                }
                else
                {
                    observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                    observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where?.OperationFault is { } whereFault ? (whereFault, default) : where is null ? (null, observableDictionaryQueryCount > 0) : observableDictionaryQuery.OperationFault is { } observableDictionaryQueryFault ? (observableDictionaryQueryFault, default) : (null, where.Count > 0);

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        observableDictionaryQueryCount = e.Action is NotifyDictionaryChangedAction.Reset ? observableDictionaryQuery.Count : observableDictionaryQueryCount + (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        Evaluate();
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableDictionaryQuery<,>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        if (Predicate is null)
        {
            observableDictionaryQueryCount = observableDictionaryQuery.Count;
            observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
            observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        }
        else
        {
            where = observableDictionaryQuery.ObserveWhere(Predicate);
            ((INotifyDictionaryChanged<TKey, TValue>)where).DictionaryChanged += WhereDictionaryChanged;
            where.PropertyChanged += WherePropertyChanged;
        }
        Evaluate();
    }

    public override string ToString() =>
        Predicate is null ? $"any of {observableDictionaryQuery}" : $"any of {observableDictionaryQuery} matching {Predicate}";

    void WhereDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        Evaluate();

    void WherePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableDictionaryQuery<,>.OperationFault))
            Evaluate();
    }
}
