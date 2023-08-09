namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryAllQuery<TKey, TValue> :
    ObservableDictionaryScalarQuery<TKey, TValue, bool>
    where TKey : notnull
{
    public ObservableDictionaryAllQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, Expression<Func<TKey, TValue, bool>> predicate) :
        base(collectionObserver, observableDictionaryQuery) =>
        Predicate = predicate;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableDictionaryQuery<TKey, TValue>? where;
    int observableDictionaryQueryCount;

    internal readonly Expression<Func<TKey, TValue, bool>> Predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removeFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removeFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
                where!.DictionaryChanged -= WhereDictionaryChanged;
                where.Dispose();
            }
            return removeFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = where!.OperationFault is { } whereFault ? (whereFault, default) : (null, observableDictionaryQueryCount == where.Count);

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        observableDictionaryQueryCount = e.Action is NotifyDictionaryChangedAction.Reset ? observableDictionaryQuery.Count : observableDictionaryQueryCount + (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        Evaluate();
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableDictionaryQuery<TKey, TValue>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableDictionaryQueryCount = observableDictionaryQuery.Count;
        where = observableDictionaryQuery.ObserveWhere(Predicate);
        where.DictionaryChanged += WhereDictionaryChanged;
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    void WhereDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        Evaluate();
}
