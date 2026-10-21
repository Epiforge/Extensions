namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryValueForQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, TKey key, bool notFoundIsDefault) :
    ObservableDictionaryScalarQuery<TKey, TValue, TValue>(collectionObserver, observableDictionaryQuery)
    where TKey : notnull
{
    internal readonly TKey Key = key;
    internal readonly bool NotFoundIsDefault = notFoundIsDefault;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = observableDictionaryQuery.OperationFault is { } fault ? (fault, default!) : observableDictionaryQuery.TryGetValue(Key, out var value) ? (null, value) : NotFoundIsDefault ? (null, default!) : (OwnFault(ExceptionHelper.KeyNotFound), default!);

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var changeHold = HoldOwnChanges();
        if (e.Action is NotifyDictionaryChangedAction.Reset || Names(e.OldItems) || Names(e.NewItems))
            Evaluate();
    }

    /// <summary>
    /// Determines whether a change names this query's key, by the comparer of the dictionary it reads, since a change may name a key the dictionary considers equal to it without being the same
    /// </summary>
    bool Names(IReadOnlyList<KeyValuePair<TKey, TValue>>? keyValuePairs)
    {
        if (keyValuePairs is null)
            return false;
        var keyComparer = observableDictionaryQuery.KeyComparer;
        for (int i = 0, ii = keyValuePairs.Count; i < ii; ++i)
            if (keyComparer.Equals(keyValuePairs[i].Key, Key))
                return true;
        return false;
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableDictionaryQuery<,>.OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            Evaluate();
        }
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(observableDictionaryQuery);
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"value for {Key} in {observableDictionaryQuery} or {(NotFoundIsDefault ? "default" : "fault")} if not found";
}
