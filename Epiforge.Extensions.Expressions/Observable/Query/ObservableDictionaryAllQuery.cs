namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryAllQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, Expression<Func<TKey, TValue, bool>> predicate) :
    ObservableDictionaryScalarQuery<TKey, TValue, bool>(collectionObserver, observableDictionaryQuery)
    where TKey : notnull
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableDictionaryQuery<TKey, TValue>? where;
    int observableDictionaryQueryCount;

    internal readonly Expression<Func<TKey, TValue, bool>> Predicate = predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removeFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removeFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
                ((INotifyDictionaryChanged<TKey, TValue>)where!).DictionaryChanged -= WhereDictionaryChanged;
                where.Dispose();
                RemovedFromCache();
            }
            return removeFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where!.OperationFault is { } whereFault ? (whereFault, default) : (null, observableDictionaryQueryCount == where.Count);

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var changeHold = HoldOwnChanges();
        observableDictionaryQueryCount = e.Action is NotifyDictionaryChangedAction.Reset ? observableDictionaryQuery.Count : observableDictionaryQueryCount + (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        Evaluate();
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        if (e.PropertyName == nameof(ObservableDictionaryQuery<,>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        where = observableDictionaryQuery.ObserveWhere(Predicate);
        using var changeHold = HoldChangesOf(observableDictionaryQuery, Unscoped(where));
        observableDictionaryQueryCount = observableDictionaryQuery.Count;
        ((INotifyDictionaryChanged<TKey, TValue>)where).DictionaryChanged += WhereDictionaryChanged;
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"all {observableDictionaryQuery} matching {Predicate}";

    void WhereDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var changeHold = HoldOwnChanges();
        Evaluate();
    }
}
