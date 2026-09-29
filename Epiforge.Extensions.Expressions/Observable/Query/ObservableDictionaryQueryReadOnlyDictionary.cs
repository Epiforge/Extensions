namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>(CollectionObserver collectionObserver, IReadOnlyDictionary<TKey, TValue> readOnlyDictionary) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    static readonly bool isValueType = typeof(TValue).IsValueType;

    Dictionary<TKey, TValue>? copy;
    internal readonly IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionary = readOnlyDictionary;

    public override TValue this[TKey key] =>
        copy is { } kept ? kept[key] : ReadOnlyDictionary[key];

    public override int Count =>
        copy?.Count ?? ReadOnlyDictionary.Count;

    internal override IEqualityComparer<TKey> KeyComparer =>
        ReadOnlyDictionary switch
        {
            IHashKeys<TKey> hashKeys => hashKeys.Comparer,
            Dictionary<TKey, TValue> dictionary => dictionary.Comparer,
            _ => EqualityComparer<TKey>.Default
        };

    public override IEnumerable<TKey> Keys =>
        copy is { } kept ? kept.Keys : ReadOnlyDictionary.Keys;

    public override IEnumerable<TValue> Values =>
        copy is { } kept ? kept.Values : ReadOnlyDictionary.Values;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset || (e.NewItems is null && e.OldItems is null))
        {
            Reconcile();
            return;
        }
        var keys = new List<TKey>();
        if (e.OldItems is { } oldItems)
            foreach (var item in oldItems)
                if (item is KeyValuePair<TKey, TValue> keyValuePair)
                    keys.Add(keyValuePair.Key);
                else
                {
                    Reconcile();
                    return;
                }
        if (e.NewItems is { } newItems)
            foreach (var item in newItems)
                if (item is KeyValuePair<TKey, TValue> keyValuePair)
                    keys.Add(keyValuePair.Key);
                else
                {
                    Reconcile();
                    return;
                }
        Reconcile(keys);
    }

    public override bool Contains(KeyValuePair<TKey, TValue> item) =>
        copy is { } kept ? ((ICollection<KeyValuePair<TKey, TValue>>)kept).Contains(item) : ReadOnlyDictionary.Contains(item);

    public override bool ContainsKey(TKey key) =>
        copy is { } kept ? kept.ContainsKey(key) : ReadOnlyDictionary.ContainsKey(key);

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (copy is { } kept)
            ((ICollection<KeyValuePair<TKey, TValue>>)kept).CopyTo(array, arrayIndex);
        else if (ReadOnlyDictionary is ICollection<KeyValuePair<TKey, TValue>> collection)
            collection.CopyTo(array, arrayIndex);
        else
            throw new NotSupportedException();
    }

    void DictionaryChangedNotifierDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset)
        {
            Reconcile();
            return;
        }
        if (Follow(e))
            return;
        var keys = new List<TKey>(e.OldItems.Count + e.NewItems.Count);
        foreach (var keyValuePair in e.OldItems)
            keys.Add(keyValuePair.Key);
        foreach (var keyValuePair in e.NewItems)
            keys.Add(keyValuePair.Key);
        Reconcile(keys);
    }

    void DictionaryChangedNotifierDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset)
        {
            Reconcile();
            return;
        }
        var keys = new List<TKey>(e.OldItems.Count + e.NewItems.Count);
        foreach (var keyValuePair in e.OldItems)
            keys.Add((TKey)keyValuePair.Key!);
        foreach (var keyValuePair in e.NewItems)
            keys.Add((TKey)keyValuePair.Key!);
        Reconcile(keys);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
                    dictionaryChangedNotifier.DictionaryChanged -= DictionaryChangedNotifierDictionaryChanged;
                else if (ReadOnlyDictionary is INotifyDictionaryChanged dictionaryChangedNotifierBoxed)
                    dictionaryChangedNotifierBoxed.DictionaryChanged -= DictionaryChangedNotifierDictionaryChangedBoxed;
                else if (ReadOnlyDictionary is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                else
                {
                    if (ReadOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
                        propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                    if (ReadOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
                        propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                }
            }
            return removedFromCache;
        }
        return true;
    }

    /// <summary>
    /// Applies a change naming one key to the copy and announces it as the dictionary did, provided the dictionary and the copy agree that it is the change still outstanding for that key, which they do whenever notifications arrive in the order of the changes they describe
    /// </summary>
    /// <returns><c>true</c> if the change was applied; otherwise, <c>false</c>, and the key must be reconciled instead</returns>
    bool Follow(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var changeHold = HoldOwnChanges();
        var kept = copy!;
        switch (e.Action)
        {
            case NotifyDictionaryChangedAction.Add when e.NewItems.Count is 1 && e.OldItems.Count is 0:
                {
                    var (key, value) = e.NewItems[0];
                    if (kept.ContainsKey(key) || !ReadOnlyDictionary.TryGetValue(key, out var present) || !IsSame(present, value))
                        return false;
                    OnPropertyChanging(countPropertyChangingEventArgs);
                    kept.Add(key, value);
                    OnPropertyChanged(countPropertyChangedEventArgs);
                    break;
                }
            case NotifyDictionaryChangedAction.Remove when e.OldItems.Count is 1 && e.NewItems.Count is 0:
                {
                    var (key, value) = e.OldItems[0];
                    if (ReadOnlyDictionary.ContainsKey(key) || !kept.TryGetValue(key, out var keptValue) || !IsSame(keptValue, value))
                        return false;
                    OnPropertyChanging(countPropertyChangingEventArgs);
                    kept.Remove(key);
                    OnPropertyChanged(countPropertyChangedEventArgs);
                    break;
                }
            case NotifyDictionaryChangedAction.Replace when e.NewItems.Count is 1 && e.OldItems.Count is 1:
                {
                    var (key, value) = e.NewItems[0];
                    var (oldKey, oldValue) = e.OldItems[0];
                    if (!KeyComparer.Equals(key, oldKey) || IsSame(value, oldValue) || !ReadOnlyDictionary.TryGetValue(key, out var present) || !IsSame(present, value) || !kept.TryGetValue(key, out var keptValue) || !IsSame(keptValue, oldValue))
                        return false;
                    kept[key] = value;
                    break;
                }
            default:
                return false;
        }
        OnChanged(e);
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<KeyValuePair<TKey, TValue>>)kept).GetEnumerator() : ReadOnlyDictionary.GetEnumerator();

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        if (copy is { } kept)
        {
            var range = new List<KeyValuePair<TKey, TValue>>();
            foreach (var key in keys)
                if (kept.TryGetValue(key, out var value))
                    range.Add(new KeyValuePair<TKey, TValue>(key, value));
            return range.AsReadOnly();
        }
        if (ReadOnlyDictionary is IReadOnlyRangeDictionary<TKey, TValue> rangeDictionary)
            return rangeDictionary.GetRange(keys);
        else
            throw new NotSupportedException();
    }

    static bool IsSame(TValue value, TValue otherValue) =>
        isValueType ? EqualityComparer<TValue>.Default.Equals(value, otherValue) : ReferenceEquals(value, otherValue);

    protected override void OnInitialization()
    {
        if (ReadOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
        {
            copy = new(ReadOnlyDictionary, KeyComparer);
            dictionaryChangedNotifier.DictionaryChanged += DictionaryChangedNotifierDictionaryChanged;
        }
        else if (ReadOnlyDictionary is INotifyDictionaryChanged dictionaryChangedNotifierBoxed)
        {
            copy = new(ReadOnlyDictionary, KeyComparer);
            dictionaryChangedNotifierBoxed.DictionaryChanged += DictionaryChangedNotifierDictionaryChangedBoxed;
        }
        else if (ReadOnlyDictionary is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = new(ReadOnlyDictionary, KeyComparer);
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
        else
        {
            if (ReadOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
                propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
            if (ReadOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        }
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<,>.Count))
        {
            using var changeHold = HoldOwnChanges();
            OnPropertyChanged(e);
        }
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyDictionary<,>.Count))
        {
            using var changeHold = HoldOwnChanges();
            OnPropertyChanging(e);
        }
    }

    /// <summary>
    /// Brings the copy into agreement with the dictionary for each key a change named, reading each from the dictionary rather than from the change, so that changes announced out of the order they were made still leave the copy agreeing with the dictionary
    /// </summary>
    void Reconcile(List<TKey> keys)
    {
        using var changeHold = HoldOwnChanges();
        var kept = copy!;
        List<KeyValuePair<TKey, TValue>>? added = null, removed = null, replacements = null, replaced = null;
        HashSet<TKey>? seen = keys.Count > 1 ? new(KeyComparer) : null;
        foreach (var key in keys)
        {
            if (seen is not null && !seen.Add(key))
                continue;
            var isPresent = ReadOnlyDictionary.TryGetValue(key, out var value);
            var wasPresent = kept.TryGetValue(key, out var keptValue);
            if (isPresent && !wasPresent)
                (added ??= []).Add(new(key, value!));
            else if (isPresent && !IsSame(value!, keptValue!))
            {
                (replacements ??= []).Add(new(key, value!));
                (replaced ??= []).Add(new(key, keptValue!));
            }
            else if (!isPresent && wasPresent)
                (removed ??= []).Add(new(key, keptValue!));
        }
        var countChanges = (added?.Count ?? 0) != (removed?.Count ?? 0);
        if (countChanges)
            OnPropertyChanging(countPropertyChangingEventArgs);
        if (removed is not null)
            foreach (var keyValuePair in removed)
                kept.Remove(keyValuePair.Key);
        if (replacements is not null)
            foreach (var keyValuePair in replacements)
                kept[keyValuePair.Key] = keyValuePair.Value;
        if (added is not null)
            foreach (var keyValuePair in added)
                kept.Add(keyValuePair.Key, keyValuePair.Value);
        if (countChanges)
            OnPropertyChanged(countPropertyChangedEventArgs);
        if (removed is not null)
            OnChanged(new NotifyDictionaryChangedEventArgs<TKey, TValue>(NotifyDictionaryChangedAction.Remove, removed));
        if (replacements is not null)
            OnChanged(new NotifyDictionaryChangedEventArgs<TKey, TValue>(NotifyDictionaryChangedAction.Replace, replacements, replaced));
        if (added is not null)
            OnChanged(new NotifyDictionaryChangedEventArgs<TKey, TValue>(NotifyDictionaryChangedAction.Add, added));
    }

    void Reconcile()
    {
        using var changeHold = HoldOwnChanges();
        var reread = new Dictionary<TKey, TValue>(ReadOnlyDictionary, KeyComparer);
        var countChanges = reread.Count != copy!.Count;
        if (countChanges)
            OnPropertyChanging(countPropertyChangingEventArgs);
        copy = reread;
        if (countChanges)
            OnPropertyChanged(countPropertyChangedEventArgs);
        OnChanged(new NotifyDictionaryChangedEventArgs<TKey, TValue>(NotifyDictionaryChangedAction.Reset));
    }

    public override string ToString() =>
        $"read-only dictionary of {typeof(TKey).FullName}/{typeof(TValue).FullName} key/value pairs (hash code {ReadOnlyDictionary.GetHashCode()})";

    public override bool TryGetValue(TKey key, out TValue value) =>
        copy is { } kept ? kept.TryGetValue(key, out value!) : ReadOnlyDictionary.TryGetValue(key, out value!);
}
