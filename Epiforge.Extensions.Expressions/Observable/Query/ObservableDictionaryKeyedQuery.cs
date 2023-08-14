namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryKeyedQuery<TKey, TValue> :
    ObservableDictionaryScalarQuery<TKey, TValue, KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    public ObservableDictionaryKeyedQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, IComparer<TKey>? keyComparer, bool notFoundIsDefault) :
        base(collectionObserver, observableDictionaryQuery)
    {
        KeyComparer = keyComparer;
        NotFoundIsDefault = notFoundIsDefault;
    }

    SortedDictionary<TKey, TValue>? sortedDictionary;

    internal readonly IComparer<TKey>? KeyComparer;
    internal readonly bool NotFoundIsDefault;

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

    void Evaluate()
    {
        var faultList = new FaultList();
        faultList.Check(observableDictionaryQuery);
        KeyValuePair<TKey, TValue> result = default;
        var count = sortedDictionary?.Count ?? observableDictionaryQuery.Count;
        if (count == 0)
        {
            if (!NotFoundIsDefault)
                faultList.Add(ExceptionHelper.SequenceContainsNoElements);
        }
        else if (count == 1 || KeyComparer is not null)
            result = (sortedDictionary ?? (IEnumerable<KeyValuePair<TKey, TValue>>)observableDictionaryQuery).First();
        else
            faultList.Add(ExceptionHelper.SequenceContainsMoreThanOneElement);
        Evaluation = (faultList.Fault, result);
    }

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset)
        {
            if (KeyComparer is { } keyComparer)
            {
                sortedDictionary = new SortedDictionary<TKey, TValue>(keyComparer);
                foreach (var keyValuePair in observableDictionaryQuery)
                    sortedDictionary.Add(keyValuePair.Key, keyValuePair.Value);
            }
            Evaluate();
        }
        else if (sortedDictionary is not null)
        {
            var reevaluate = sortedDictionary.Count == 0 && e.NewItems?.Count > 0 || e.NewItems?.Count == 0 && e.OldItems?.Count == sortedDictionary.Count;
            TKey previousLeadingKey = default!;
            var previousLeadingKeySet = false;
            if (!reevaluate)
            {
                previousLeadingKey = sortedDictionary.First().Key;
                previousLeadingKeySet = true;
            }
            if (e.OldItems is { } oldItems)
                foreach (var oldKeyValuePair in oldItems)
                    sortedDictionary!.Remove(oldKeyValuePair.Key);
            if (e.NewItems is { } newItems)
                foreach (var newKeyValuePair in newItems)
                    sortedDictionary!.Add(newKeyValuePair.Key, newKeyValuePair.Value);
            if (previousLeadingKeySet)
                reevaluate = KeyComparer!.Compare(previousLeadingKey, sortedDictionary.First().Key) != 0;
            if (reevaluate)
                Evaluate();
        }
        else
            Evaluate();
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableDictionaryQuery<TKey, TValue>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        if (KeyComparer is { } keyComparer)
        {
            sortedDictionary = new SortedDictionary<TKey, TValue>(keyComparer);
            foreach (var keyValuePair in observableDictionaryQuery)
                sortedDictionary.Add(keyValuePair.Key, keyValuePair.Value);
        }
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"first key/value pair in {observableDictionaryQuery}{(KeyComparer is null ? string.Empty : $" sorted by {KeyComparer}")} or {(NotFoundIsDefault ? "default" : "fault")} if not found";
}
