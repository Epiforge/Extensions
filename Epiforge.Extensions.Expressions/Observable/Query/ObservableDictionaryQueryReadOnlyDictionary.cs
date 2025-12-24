namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>(CollectionObserver collectionObserver, IReadOnlyDictionary<TKey, TValue> readOnlyDictionary) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    internal readonly IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionary = readOnlyDictionary;

    public override TValue this[TKey key] =>
        ReadOnlyDictionary[key];

    public override int Count =>
        ReadOnlyDictionary.Count;

    public override IEnumerable<TKey> Keys =>
        ReadOnlyDictionary.Keys;

    public override IEnumerable<TValue> Values =>
        ReadOnlyDictionary.Values;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    public override bool Contains(KeyValuePair<TKey, TValue> item) =>
        ReadOnlyDictionary.Contains(item);

    public override bool ContainsKey(TKey key) =>
        ReadOnlyDictionary.ContainsKey(key);

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (ReadOnlyDictionary is ICollection<KeyValuePair<TKey, TValue>> collection)
            collection.CopyTo(array, arrayIndex);
        else
            throw new NotSupportedException();
    }

    void DictionaryChangedNotifierDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void DictionaryChangedNotifierDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (ReadOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
                    propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                if (ReadOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
                    propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                if (ReadOnlyDictionary is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                if (ReadOnlyDictionary is INotifyDictionaryChanged dictionaryChangedNotifierBoxed)
                    dictionaryChangedNotifierBoxed.DictionaryChanged -= DictionaryChangedNotifierDictionaryChangedBoxed;
                if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
                    dictionaryChangedNotifier.DictionaryChanged -= DictionaryChangedNotifierDictionaryChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        ReadOnlyDictionary.GetEnumerator();

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        if (ReadOnlyDictionary is IRangeDictionary<TKey, TValue> rangeDictionary)
            return rangeDictionary.GetRange(keys);
        else
            throw new NotSupportedException();
    }

    protected override void OnInitialization()
    {
        if (ReadOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
            propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
        if (ReadOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        if (ReadOnlyDictionary is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        if (ReadOnlyDictionary is INotifyDictionaryChanged dictionaryChangedNotifierBoxed)
            dictionaryChangedNotifierBoxed.DictionaryChanged += DictionaryChangedNotifierDictionaryChangedBoxed;
        if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
            dictionaryChangedNotifier.DictionaryChanged += DictionaryChangedNotifierDictionaryChanged;
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<,>.Count))
            OnPropertyChanged(e);
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<,>.Count))
            OnPropertyChanging(e);
    }

    public override string ToString() =>
        $"read-only dictionary of {typeof(TKey).FullName}/{typeof(TValue).FullName} key/value pairs (hash code {ReadOnlyDictionary.GetHashCode()})";

    public override bool TryGetValue(TKey key, out TValue value) =>
        ReadOnlyDictionary.TryGetValue(key, out value!);
}
