namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryConcurrentQuery<TKey, TValue> :
    ObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    public ObservableDictionaryConcurrentQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source) :
        base(collectionObserver) =>
        this.source = source;

    ObservableConcurrentDictionary<TKey, TValue>? observableConcurrentDictionary;

    readonly ObservableDictionaryQuery<TKey, TValue> source;

    public override TValue this[TKey key] =>
        observableConcurrentDictionary![key];

    public override int Count =>
        observableConcurrentDictionary!.Count;

    public override IEnumerable<TKey> Keys =>
        observableConcurrentDictionary!.Keys;

    public override IEnumerable<TValue> Values =>
        observableConcurrentDictionary!.Values;

    public override bool ContainsKey(TKey key) =>
        observableConcurrentDictionary!.ContainsKey(key);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.DictionaryChanged -= SourceDictionaryChanged;
                observableConcurrentDictionary!.DictionaryChanged -= ObservableConcurrentDictionaryDictionaryChanged;
                observableConcurrentDictionary.PropertyChanged -= ObservableConcurrentDictionaryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        observableConcurrentDictionary!.GetEnumerator();

    void ObservableConcurrentDictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void ObservableConcurrentDictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            OperationFault = source.OperationFault;
    }

    protected override void OnInitialization()
    {
        observableConcurrentDictionary = new ObservableConcurrentDictionary<TKey, TValue>(source);
        OperationFault = source.OperationFault;
        source.DictionaryChanged += SourceDictionaryChanged;
        observableConcurrentDictionary.DictionaryChanged += ObservableConcurrentDictionaryDictionaryChanged;
        observableConcurrentDictionary.PropertyChanged += ObservableConcurrentDictionaryPropertyChanged;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset)
            observableConcurrentDictionary!.Reset(source);
        else
        {
            if (e.OldItems is { } oldItems && oldItems.Count > 0)
                foreach (var oldKeyValuePair in e.OldItems)
                    observableConcurrentDictionary!.TryRemove(oldKeyValuePair.Key, out _);
            if (e.NewItems is { } newItems && newItems.Count > 0)
                foreach (var newKeyValuePair in e.NewItems)
                    observableConcurrentDictionary!.TryAdd(newKeyValuePair.Key, newKeyValuePair.Value);
        }
    }

    public override string ToString() =>
        $"conversion of {source} to a concurrent dictionary";

    public override bool TryGetValue(TKey key, out TValue value) =>
        observableConcurrentDictionary!.TryGetValue(key, out value);
}
