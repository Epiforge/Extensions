namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ScopedObservableLookupQuery<TKey, TElement> :
    ScopedObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IObservableLookupQuery<TKey, TElement>
    where TKey : notnull
{
    internal ScopedObservableLookupQuery(ObservableCollectionLookupQuery<TKey, TElement> lookup) :
        base(lookup)
    {
        this.lookup = lookup;
        ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)this.lookup).DictionaryChanged += LookupDictionaryChanged;
        ((INotifyDictionaryChanged)this.lookup).DictionaryChanged += LookupDictionaryChangedBoxed;
    }

    readonly ObservableCollectionLookupQuery<TKey, TElement> lookup;

    public override void Dispose()
    {
        ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)lookup).DictionaryChanged -= LookupDictionaryChanged;
        ((INotifyDictionaryChanged)lookup).DictionaryChanged -= LookupDictionaryChangedBoxed;
        base.Dispose();
    }

    event EventHandler<NotifyDictionaryChangedEventArgs<TKey, IObservableGrouping<TKey, TElement>>>? dictionaryChanged;
    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? boxedDictionaryChanged;

    event EventHandler<NotifyDictionaryChangedEventArgs<TKey, IObservableGrouping<TKey, TElement>>>? INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>.DictionaryChanged
    {
        add => dictionaryChanged += value;
        remove => dictionaryChanged -= value;
    }

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => boxedDictionaryChanged += value;
        remove => boxedDictionaryChanged -= value;
    }

    void LookupDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, IObservableGrouping<TKey, TElement>> e) =>
        dictionaryChanged?.Invoke(this, e);

    void LookupDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        boxedDictionaryChanged?.Invoke(this, e);

    public IObservableGrouping<TKey, TElement> this[TKey key] =>
        lookup[key];

    IObservableGrouping<TKey, TElement> IDictionary<TKey, IObservableGrouping<TKey, TElement>>.this[TKey key]
    {
        get => ((IDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup)[key];
        set => ((IDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup)[key] = value;
    }

    public bool IsReadOnly =>
        lookup.IsReadOnly;

    public ICollection<TKey> Keys =>
        lookup.Keys;

    IEnumerable<TKey> IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        ((IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Keys;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        ((IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Keys;

    public ICollection<IObservableGrouping<TKey, TElement>> Values =>
        lookup.Values;

    IEnumerable<IObservableGrouping<TKey, TElement>> IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        ((IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Values;

    IEnumerable<IObservableGrouping<TKey, TElement>> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        ((IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Values;

    public bool ContainsKey(TKey key) =>
        lookup.ContainsKey(key);

    public void CopyTo(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>[] array, int arrayIndex) =>
        lookup.CopyTo(array, arrayIndex);

    public IReadOnlyList<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> GetRange(IEnumerable<TKey> keys) =>
        lookup.GetRange(keys);

    public bool TryGetValue(TKey key, out IObservableGrouping<TKey, TElement> value) =>
        lookup.TryGetValue(key, out value);

    IEnumerator<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.GetEnumerator() =>
        ((IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>)lookup).GetEnumerator();

    void IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Add(TKey key, IObservableGrouping<TKey, TElement> value) =>
        ((IDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Add(key, value);

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Add(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        ((ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>)lookup).Add(item);

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Clear() =>
        ((ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>)lookup).Clear();

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Contains(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        ((ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>)lookup).Contains(item);

    bool IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Remove(TKey key) =>
        ((IDictionary<TKey, IObservableGrouping<TKey, TElement>>)lookup).Remove(key);

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Remove(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        ((ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>)lookup).Remove(item);

}
