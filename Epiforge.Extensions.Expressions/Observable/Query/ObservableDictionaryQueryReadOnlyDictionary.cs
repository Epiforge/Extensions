namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue> :
    ObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    public ObservableDictionaryQueryReadOnlyDictionary(CollectionObserver collectionObserver, IReadOnlyDictionary<TKey, TValue> readOnlyDictionary) :
        base(collectionObserver) =>
        ReadOnlyDictionary = readOnlyDictionary;

    internal readonly IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionary;

    public override TValue this[TKey key] =>
        ReadOnlyDictionary[key];

    public override int Count =>
        ReadOnlyDictionary.Count;

    public override IEnumerable<TKey> Keys =>
        ReadOnlyDictionary.Keys;

    public override IEnumerable<TValue> Values =>
        ReadOnlyDictionary.Values;

    public override bool ContainsKey(TKey key) =>
        ReadOnlyDictionary.ContainsKey(key);

    void DictionaryChangedNotifierDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

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
                if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
                    dictionaryChangedNotifier.DictionaryChanged -= DictionaryChangedNotifierDictionaryChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        ReadOnlyDictionary.GetEnumerator();

    protected override void OnInitialization()
    {
        if (ReadOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
            propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
        if (ReadOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
            dictionaryChangedNotifier.DictionaryChanged += DictionaryChangedNotifierDictionaryChanged;
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<TKey, TValue>.Count))
            OnPropertyChanged(e);
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<TKey, TValue>.Count))
            OnPropertyChanging(e);
    }

    public override string ToString() =>
        $"read-only dictionary of {typeof(TKey).FullName}/{typeof(TValue).FullName} key/value pairs (hash code {ReadOnlyDictionary.GetHashCode()})";

    public override bool TryGetValue(TKey key, out TValue value) =>
        ReadOnlyDictionary.TryGetValue(key, out value!);
}
