namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Converts each value of the dictionary query it is built over to a type, either casting it or keeping it only where it is of the type, keeping the results and nothing per value besides, since a conversion's result is fixed by the value and nothing about a value can change it
/// </summary>
/// <remarks>
/// A cast was a projection by lambdas doing the cast, and a type filter was a filter by a lambda testing the type followed by that projection, each observing every value as a projection or a filter must, though nothing those observations could hear would change what they yielded. This keeps their results, faults and announcements: a value which cannot be cast is left out and is a fault of its own, named by its key; a value which is not of the type is left out of a type filter without a fault; and the results look keys up by the source's comparer
/// </remarks>
sealed class ObservableDictionaryConversionQuery<TKey, TValue, TResult>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, bool isFilter) :
    ObservableDictionaryQuery<TKey, TResult>(collectionObserver)
    where TKey : notnull
{
    readonly object access = new();
    IReadOnlyList<KeyValuePair<TKey, TResult>>? enumerationSnapshot;
    /// <summary>
    /// Whether this query is following a change of its source, during which it delivers a notification for each key it changes, so that a change its subscribers make to the source in the meantime waits until this one has been followed
    /// </summary>
    bool followingSourceChange;
    internal readonly bool IsFilter = isFilter;
    IReadOnlyList<TKey>? keysSnapshot;
    /// <summary>
    /// The changes the source announced while this query was following another, in the order they were announced, made with the first of them
    /// </summary>
    Queue<NotifyDictionaryChangedEventArgs<TKey, TValue>>? pendingSourceChanges;
    bool released;
    /// <summary>
    /// The results, made when the query is initialized so that they can be sized for the source
    /// </summary>
    ObservableDictionary<TKey, TResult> result = null!;
    IReadOnlyList<TResult>? valuesSnapshot;

    public override TResult this[TKey key]
    {
        get
        {
            lock (access)
                return result[key];
        }
    }

    private protected override int CurrentCount
    {
        get
        {
            lock (access)
                return result.Count;
        }
    }

    internal override IEqualityComparer<TKey> KeyComparer =>
        source.KeyComparer;

    public override IEnumerable<TKey> Keys
    {
        get
        {
            lock (access)
                return keysSnapshot ??= result.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TResult> Values
    {
        get
        {
            lock (access)
                return valuesSnapshot ??= result.Values.ToList().AsReadOnly();
        }
    }

    public override bool Contains(KeyValuePair<TKey, TResult> item)
    {
        lock (access)
            return result.Contains(item);
    }

    public override bool ContainsKey(TKey key)
    {
        lock (access)
            return result.ContainsKey(key);
    }

    /// <summary>
    /// Converts a value into the results it is given, adding its fault where it has one to a list made on the first fault, which begins with the fault the query already reports where the conversion follows a change rather than starting the results over
    /// </summary>
    void ConvertInto(KeyValuePair<TKey, TValue> keyValuePair, ObservableDictionary<TKey, TResult> into, ref FaultList? faultList, Exception? standingFault)
    {
        if (TryConvert(keyValuePair.Value, out var converted, out var fault))
            into.Add(keyValuePair.Key, converted);
        else if (fault is not null)
            (faultList ??= new(standingFault)).Add(new EvaluationFaultException(keyValuePair.Key, fault));
    }

    public override void CopyTo(KeyValuePair<TKey, TResult>[] array, int arrayIndex)
    {
        lock (access)
            ((ICollection<KeyValuePair<TKey, TResult>>)result).CopyTo(array, arrayIndex);
    }

    void DiscardSnapshots()
    {
        enumerationSnapshot = null;
        keysSnapshot = null;
        valuesSnapshot = null;
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                lock (access)
                {
                    released = true;
                    source.DictionaryChanged -= SourceDictionaryChanged;
                    source.PropertyChanged -= SourcePropertyChanged;
                    result.CollectionChanged -= ResultCollectionChanged;
                    ((INotifyDictionaryChanged)result).DictionaryChanged -= ResultDictionaryChangedBoxed;
                    result.DictionaryChanged -= ResultDictionaryChanged;
                    result.PropertyChanging -= ResultPropertyChanging;
                    result.PropertyChanged -= ResultPropertyChanged;
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TResult>> GetEnumerator()
    {
        lock (access)
            return (enumerationSnapshot ??= result.ToList().AsReadOnly()).GetEnumerator();
    }

    public override IReadOnlyList<KeyValuePair<TKey, TResult>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return result.GetRange(keys);
    }

    private protected override void OnChangeObservationBegan(ObservableDictionaryChangeObservation observation)
    {
        switch (observation)
        {
            case ObservableDictionaryChangeObservation.BoxedDictionary:
                ((INotifyDictionaryChanged)result).DictionaryChanged += ResultDictionaryChangedBoxed;
                break;
            case ObservableDictionaryChangeObservation.Collection:
                result.CollectionChanged += ResultCollectionChanged;
                break;
            case ObservableDictionaryChangeObservation.Dictionary:
                result.DictionaryChanged += ResultDictionaryChanged;
                break;
        }
    }

    private protected override void OnChangeObservationEnded(ObservableDictionaryChangeObservation observation)
    {
        switch (observation)
        {
            case ObservableDictionaryChangeObservation.BoxedDictionary:
                ((INotifyDictionaryChanged)result).DictionaryChanged -= ResultDictionaryChangedBoxed;
                break;
            case ObservableDictionaryChangeObservation.Collection:
                result.CollectionChanged -= ResultCollectionChanged;
                break;
            case ObservableDictionaryChangeObservation.Dictionary:
                result.DictionaryChanged -= ResultDictionaryChanged;
                break;
        }
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            FaultList? faultList = null;
            result = new(source.Count, source.KeyComparer);
            foreach (var keyValuePair in source)
                ConvertInto(keyValuePair, result, ref faultList, null);
            OperationFault = faultList?.Fault;
            InheritOperationFault(source.OperationFault);
            source.DictionaryChanged += SourceDictionaryChanged;
            source.PropertyChanged += SourcePropertyChanged;
            result.PropertyChanging += ResultPropertyChanging;
            result.PropertyChanged += ResultPropertyChanged;
        }
    }

    void ResultCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ResultDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TResult> e) =>
        OnDictionaryChanged(e);

    void ResultDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    void ResultPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ResultPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    /// <summary>
    /// Follows a change of the source one key at a time, delivering the notification of each key before taking the next, so that a subscriber finds this query as each notification leaves it
    /// </summary>
    void FollowSourceChange(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        FaultList? faultList = null;
        if (e.Action is NotifyDictionaryChangedAction.Reset)
        {
            lock (access)
            {
                if (released)
                    return;
                var newResult = new ObservableDictionary<TKey, TResult>(source.Count, source.KeyComparer);
                foreach (var keyValuePair in source)
                    ConvertInto(keyValuePair, newResult, ref faultList, null);
                DiscardSnapshots();
                result.Reset(newResult);
                OperationFault = faultList?.Fault;
            }
            return;
        }
        var keyComparer = source.KeyComparer;
        foreach (var keyValuePair in e.OldItems)
        {
            if (TryConvert(keyValuePair.Value, out _, out var fault))
            {
                lock (access)
                {
                    if (released)
                        return;
                    DiscardSnapshots();
                    result.Remove(keyValuePair.Key);
                }
                RaiseNotificationsDeferredSoFar();
            }
            else if (fault is not null)
                lock (access)
                    (faultList ??= new(OwnOperationFault)).RemoveKey(keyValuePair.Key, keyComparer);
        }
        foreach (var keyValuePair in e.NewItems)
        {
            if (TryConvert(keyValuePair.Value, out var converted, out var fault))
            {
                lock (access)
                {
                    if (released)
                        return;
                    DiscardSnapshots();
                    result.Add(keyValuePair.Key, converted);
                }
                RaiseNotificationsDeferredSoFar();
            }
            else if (fault is not null)
                lock (access)
                    (faultList ??= new(OwnOperationFault)).Add(new EvaluationFaultException(keyValuePair.Key, fault));
        }
        if (faultList is not null)
            lock (access)
                if (!released)
                    OperationFault = faultList.Fault;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (followingSourceChange)
        {
            (pendingSourceChanges ??= new()).Enqueue(e);
            return;
        }
        followingSourceChange = true;
        try
        {
            FollowSourceChange(e);
            while (pendingSourceChanges is { Count: > 0 } pending)
                FollowSourceChange(pending.Dequeue());
        }
        finally
        {
            followingSourceChange = false;
        }
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                if (!released)
                    InheritOperationFault(source.OperationFault);
        }
    }

    public override string ToString() =>
        IsFilter ? $"{source} of type {typeof(TResult).FullName}" : $"casting {source} to {typeof(TResult).FullName}";

    /// <summary>
    /// Converts a value as the language does, yielding whether it is kept and, for a cast which cannot be made, its fault
    /// </summary>
    bool TryConvert(TValue value, out TResult converted, out Exception? fault)
    {
        fault = null;
        if (value is TResult result)
        {
            converted = result;
            return true;
        }
        converted = default!;
        if (IsFilter)
            return false;
        try
        {
            converted = (TResult)(object)value!;
            return true;
        }
        catch (Exception ex)
        {
            fault = ex;
            return false;
        }
    }

    public override bool TryGetValue(TKey key, out TResult value)
    {
        lock (access)
            return result.TryGetValue(key, out value);
    }
}
