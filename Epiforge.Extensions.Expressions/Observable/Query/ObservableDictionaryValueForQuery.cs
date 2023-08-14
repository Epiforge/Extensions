namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryValueForQuery<TKey, TValue> :
    ObservableDictionaryScalarQuery<TKey, TValue, TValue>
    where TKey : notnull
{
    public ObservableDictionaryValueForQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, TKey key, bool notFoundIsDefault) :
        base(collectionObserver, observableDictionaryQuery)
    {
        Key = key;
        NotFoundIsDefault = notFoundIsDefault;
    }

    internal readonly TKey Key;
    internal readonly bool NotFoundIsDefault;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = observableDictionaryQuery.TryGetValue(Key, out var value) ? (null, value) : NotFoundIsDefault ? (null, default!) : (ExceptionHelper.KeyNotFound, default!);

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset || e.OldItems is { } oldItems && e.OldItems.Select(kv => kv.Key).Contains(Key) || e.NewItems is { } newItems && newItems.Select(kv => kv.Key).Contains(Key))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"value for {Key} in {observableDictionaryQuery} or {(NotFoundIsDefault ? "default" : "fault")} if not found";
}
