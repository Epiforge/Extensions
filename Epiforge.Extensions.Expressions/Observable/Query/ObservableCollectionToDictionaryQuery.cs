namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionToDictionaryQuery<TElement, TKey, TValue> :
    ObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    public ObservableCollectionToDictionaryQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer) :
        base(collectionObserver)
    {
        access = new();
        dictionary = new(equalityComparer);
        duplicateKeys = new();
        this.source = source;
        KeySelector = keySelector;
        ValueSelector = valueSelector;
        EqualityComparer = equalityComparer;
    }

    readonly object access;
    readonly ObservableDictionary<TKey, TValue> dictionary;
    readonly Dictionary<TKey, int> duplicateKeys;
    int nullKeys;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<KeyValuePair<TKey, TValue>>? select;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly IEqualityComparer<TKey> EqualityComparer;
    internal readonly Expression<Func<TElement, TKey>> KeySelector;
    internal readonly Expression<Func<TElement, TValue>> ValueSelector;

    public override TValue this[TKey key]
    {
        get
        {
            lock (access)
                return dictionary[key];
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return dictionary.Count;
        }
    }

    public override IEnumerable<TKey> Keys
    {
        get
        {
            lock (access)
                return dictionary.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock(access)
                return dictionary.Values.ToList().AsReadOnly();
        }
    }

    public override bool Contains(KeyValuePair<TKey, TValue> item)
    {
        lock (access)
            return dictionary.Contains(item);
    }

    public override bool ContainsKey(TKey key)
    {
        lock (access)
            return dictionary.ContainsKey(key);
    }

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (access)
            ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);
    }

    void DictionaryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void DictionaryDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    void DictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void DictionaryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                select!.CollectionChanged -= SelectCollectionChanged;
                select.PropertyChanged -= SelectPropertyChanged;
                select.Dispose();
                dictionary.CollectionChanged -= DictionaryCollectionChanged;
                ((INotifyDictionaryChanged)dictionary).DictionaryChanged -= DictionaryDictionaryChangedBoxed;
                dictionary.DictionaryChanged -= DictionaryDictionaryChanged;
                dictionary.PropertyChanging -= DictionaryPropertyChanging;
                dictionary.PropertyChanged -= DictionaryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (access)
            foreach (var pair in dictionary)
                yield return pair;
    }

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return dictionary.GetRange(keys);
    }

    protected override void OnInitialization()
    {
        var elementParameter = Expression.Parameter(typeof(TElement));
        select = source.ObserveSelect(Expression.Lambda<Func<TElement, KeyValuePair<TKey, TValue>>>(Expression.New(typeof(KeyValuePair<TKey, TValue>).GetConstructor(new[] { typeof(TKey), typeof(TValue) })!, Expression.Invoke(KeySelector, elementParameter), Expression.Invoke(ValueSelector, elementParameter)), elementParameter));
        foreach (var keyValuePair in select)
        {
            var key = keyValuePair.Key;
            if (key is null)
                ++nullKeys;
            else if (!dictionary.TryAdd(key, keyValuePair.Value))
            {
                if (duplicateKeys.TryGetValue(key, out var count))
                    duplicateKeys[key] = count + 1;
                else
                    duplicateKeys.Add(key, 2);
            }
        }
        SetOperationFault();
        dictionary.CollectionChanged += DictionaryCollectionChanged;
        ((INotifyDictionaryChanged)dictionary).DictionaryChanged += DictionaryDictionaryChangedBoxed;
        dictionary.DictionaryChanged += DictionaryDictionaryChanged;
        dictionary.PropertyChanging += DictionaryPropertyChanging;
        dictionary.PropertyChanged += DictionaryPropertyChanged;
        select.CollectionChanged += SelectCollectionChanged;
        select.PropertyChanged += SelectPropertyChanged;
    }

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                var newDictionary = new ObservableDictionary<TKey, TValue>(EqualityComparer);
                duplicateKeys.Clear();
                nullKeys = 0;
                foreach (var keyValuePair in select!)
                {
                    var key = keyValuePair.Key;
                    if (key is null)
                        ++nullKeys;
                    else if (!newDictionary.TryAdd(key, keyValuePair.Value))
                    {
                        if (duplicateKeys.TryGetValue(key, out var count))
                            duplicateKeys[key] = count + 1;
                        else
                            duplicateKeys.Add(key, 2);
                    }
                }
                dictionary.Reset(newDictionary);
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                var newKeyValuePairs = e.NewItems?.Cast<KeyValuePair<TKey, TValue>>().ToList() ?? new List<KeyValuePair<TKey, TValue>>();
                if (e.OldItems is { } oldItems && oldItems.Count > 0 && e.OldStartingIndex >= 0)
                    foreach (var oldKeyValuePair in oldItems.Cast<KeyValuePair<TKey, TValue>>())
                    {
                        var oldKey = oldKeyValuePair.Key;
                        if (oldKey is null)
                        {
                            --nullKeys;
                            continue;
                        }
                        var newKeyValuePairIndex = newKeyValuePairs.FindIndex(keyValuePair => EqualityComparer.Equals(keyValuePair.Key, oldKey));
                        if (newKeyValuePairIndex >= 0)
                        {
                            var newKeyValuePair = newKeyValuePairs[newKeyValuePairIndex];
                            newKeyValuePairs.RemoveAt(newKeyValuePairIndex);
                            dictionary[oldKey] = newKeyValuePair.Value;
                        }
                        else if (duplicateKeys.TryGetValue(oldKey, out var count))
                        {
                            if (count == 2)
                                duplicateKeys.Remove(oldKey);
                            else
                                duplicateKeys[oldKey] = count - 1;
                        }
                        else
                            dictionary.Remove(oldKey);
                    }
                foreach (var newKeyValuePair in newKeyValuePairs)
                {
                    var newKey = newKeyValuePair.Key;
                    if (newKey is null)
                        ++nullKeys;
                    else if (!dictionary.TryAdd(newKey, newKeyValuePair.Value))
                    {
                        if (duplicateKeys.TryGetValue(newKey, out var count))
                            duplicateKeys[newKey] = count + 1;
                        else
                            duplicateKeys.Add(newKey, 2);
                    }
                }
            }
            SetOperationFault();
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<KeyValuePair<TKey, TValue>>.OperationFault))
            SetOperationFault();
    }

    void SetOperationFault()
    {
        var faultList = new FaultList();
        if (nullKeys > 0)
            faultList.AddRange(Enumerable.Range(0, nullKeys).Select(_ => ExceptionHelper.KeyNull));
        if (duplicateKeys.Count > 0)
            faultList.AddRange(Enumerable.Range(0, duplicateKeys.Keys.Sum(key => duplicateKeys[key] - 1)).Select(_ => ExceptionHelper.SameKeyAlreadyAdded));
        faultList.Check(select!);
        OperationFault = faultList.Fault;
    }

    public override string ToString() =>
        $"conversion of {source} to a dictionary mapping keys using {KeySelector} and values using {ValueSelector}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return dictionary.TryGetValue(key, out value);
    }
}
