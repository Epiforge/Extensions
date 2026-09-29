namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionLookupQuery<TKey, TElement> :
    ObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IObservableLookupQuery<TKey, TElement>,
    IObservableQueryDependent,
    ObservedElementKeys<TElement, TKey>.IOwner
    where TKey : notnull
{
    static readonly Expression<Func<IObservableGrouping<TKey, TElement>, TKey>> groupingKey = grouping => grouping.Key;

    public ObservableCollectionLookupQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) :
        base(collectionObserver)
    {
        this.source = source;
        KeySelector = keySelector;
        KeyEqualityComparer = keyEqualityComparer;
        access = new();
        collectionAndGroupingByKey = new(KeyEqualityComparer);
        groupings = [];
        groupingsQuery = this.collectionObserver.ObserveReadOnlyList(groupings);
        groupingByKey = groupingsQuery.ObserveToDictionary(groupingKey);
        keys = new(this, collectionObserver.ExpressionObserver, keySelector);
    }

    readonly object access;
    IReadOnlyList<IObservableGrouping<TKey, TElement>>? enumerationSnapshot;
    readonly Dictionary<TKey, (GroupCollection<TElement> collection, IObservableGrouping<TKey, TElement> grouping)> collectionAndGroupingByKey;
    Dictionary<TKey, List<TElement>>? elementsByKeyBeingRebuilt;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    readonly IObservableDictionaryQuery<TKey, IObservableGrouping<TKey, TElement>> groupingByKey;
    readonly ObservableRangeCollection<IObservableGrouping<TKey, TElement>> groupings;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    readonly IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> groupingsQuery;
    PropertyChangedEventHandler? keyEvaluationChangedHandler;
    readonly ObservedElementKeys<TElement, TKey> keys;
    bool released;
    readonly ObservableCollectionQuery<TElement> source;
    ObservableQuerySubscription? sourceSubscription;

    internal readonly Expression<Func<TElement, TKey>> KeySelector;
    internal readonly IEqualityComparer<TKey> KeyEqualityComparer;

    public override IObservableGrouping<TKey, TElement> this[int index]
    {
        get
        {
            lock (access)
                return groupings[index];
        }
    }

    public IObservableGrouping<TKey, TElement> this[TKey key]
    {
        get
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
            {
                if (groupingByKey.TryGetValue(key, out var existingGrouping))
                    return existingGrouping;
                var collection = collectionObserver.ExpressionObserver.Logger is { } logger ? new GroupCollection<TElement>(logger) : new GroupCollection<TElement>();
                var grouping = new ObservableGrouping<TKey, TElement>(collectionObserver, key, collectionObserver.GetObservableCollectionQuery(collection, this), this);
                grouping.Initialize();
                var collectionAndGrouping = (collection, grouping);
                collectionAndGroupingByKey.Add(key, collectionAndGrouping);
                groupings.Add(grouping);
                return grouping;
            }
        }
    }

    IObservableGrouping<TKey, TElement> IDictionary<TKey, IObservableGrouping<TKey, TElement>>.this[TKey key]
    {
        get => this[key];
        set => throw new NotSupportedException();
    }

    public override int Count
    {
        get
        {
            lock (access)
                return groupings.Count;
        }
    }

    public bool IsReadOnly =>
        true;

    /// <summary>
    /// Yields the one handler this query attaches to every key it observes, since a method group converts to a new delegate at each conversion
    /// </summary>
    PropertyChangedEventHandler ObservedElementKeys<TElement, TKey>.IOwner.KeyEvaluationChangedHandler =>
        keyEvaluationChangedHandler ??= KeyEvaluationChanged;

    public ICollection<TKey> Keys
    {
        get
        {
            lock (access)
                return groupingByKey.Keys.ToList().AsReadOnly();
        }
    }

    IEnumerable<TKey> IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        Keys;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        Keys;

    public ICollection<IObservableGrouping<TKey, TElement>> Values
    {
        get
        {
            lock (access)
                return groupingByKey.Values.ToList().AsReadOnly();

        }
    }

    IEnumerable<IObservableGrouping<TKey, TElement>> IReadOnlyRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        Values;

    IEnumerable<IObservableGrouping<TKey, TElement>> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        Values;

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => ((INotifyDictionaryChanged)groupingByKey).DictionaryChanged += value;
        remove => ((INotifyDictionaryChanged)groupingByKey).DictionaryChanged -= value;
    }

    event EventHandler<NotifyDictionaryChangedEventArgs<TKey, IObservableGrouping<TKey, TElement>>>? INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>.DictionaryChanged
    {
        add => ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)groupingByKey).DictionaryChanged += value;
        remove => ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)groupingByKey).DictionaryChanged -= value;
    }

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Add(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    void IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Add(TKey key, IObservableGrouping<TKey, TElement> value) =>
        throw new NotSupportedException();

    void AddElement(TElement element, TKey key)
    {
        GroupCollection<TElement> collection;
        if (!collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            collection = collectionObserver.ExpressionObserver.Logger is { } logger ? new(logger) : new();
            var grouping = new ObservableGrouping<TKey, TElement>(collectionObserver, key, collectionObserver.GetObservableCollectionQuery(collection, this), this);
            grouping.Initialize();
            collectionAndGrouping = (collection, grouping);
            collectionAndGroupingByKey.Add(key, collectionAndGrouping);
            groupings.Add(grouping);
        }
        else
            collection = collectionAndGrouping.collection;
        collection.Add(element);
    }

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Clear() =>
        throw new NotSupportedException();

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Contains(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    internal new IObservableLookupQuery<TKey, TElement> AsScoped() =>
        new ScopedObservableLookupQuery<TKey, TElement>(this);

    public bool ContainsKey(TKey key)
    {
        lock (access)
            return groupingByKey.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>[] array, int arrayIndex)
    {
        lock (access)
            groupingByKey.CopyTo(array, arrayIndex);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
                    released = true;
                    groupingByKey.Dispose();
                    groupingsQuery.Dispose();
                    foreach (var (_, grouping) in collectionAndGroupingByKey.Values)
                        ((ObservableGrouping<TKey, TElement>)grouping).InternalDispose();
                    groupings.CollectionChanged -= GroupingsCollectionChanged;
                    keys.Clear();
                    if (sourceSubscription is not null)
                        source.UnsubscribeDependent(sourceSubscription);
                    RemovedFromCache();
                }
            return removedFromCache;
        }
        return true;
    }

    void ObservedElementKeys<TElement, TKey>.IOwner.ElementGainedKey(TElement element, TKey key)
    {
        if (elementsByKeyBeingRebuilt is { } elementsByKey)
        {
            if (elementsByKey.TryGetValue(key, out var keyElements))
                keyElements.Add(element);
            else
                elementsByKey.Add(key, [element]);
        }
        else
            AddElement(element, key);
    }

    void ObservedElementKeys<TElement, TKey>.IOwner.ElementLostKey(TElement element, TKey key, bool isOnlyOccurrence) =>
        RemoveElement(element, key, isOnlyOccurrence);

    public override IEnumerator<IObservableGrouping<TKey, TElement>> GetEnumerator()
    {
        lock (access)
            return (enumerationSnapshot ??= groupings.ToList().AsReadOnly()).GetEnumerator();
    }

    IEnumerator<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.GetEnumerator()
    {
        lock (access)
            return groupingByKey.ToList().GetEnumerator();
    }

    public IReadOnlyList<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return groupingByKey.GetRange(keys);
    }

    void GroupingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        enumerationSnapshot = null;
        OnCollectionChanged(e);
    }

    void KeyEvaluationChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, TKey> observation || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (released)
                return;
            keys.EvaluationChanged(observation);
            TakeKeyFaultWithAccess();
        }
    }

    void ObserveSourceWithAccess()
    {
        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                keys.Add(source[i]);
        else
            foreach (var element in source)
                keys.Add(element);
    }

    protected override void OnInitialization()
    {
        groupings.CollectionChanged += GroupingsCollectionChanged;
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            ObserveSourceWithAccess();
            TakeKeyFaultWithAccess();
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            if (e.Action is NotifyCollectionChangedAction.Reset)
                RebuildWithAccess();
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } oldItems)
                    for (int i = 0, ii = oldItems.Count; i < ii; ++i)
                        keys.Remove((TElement)oldItems[i]!);
                if (e.NewItems is { } newItems)
                    for (int i = 0, ii = newItems.Count; i < ii; ++i)
                        keys.Add((TElement)newItems[i]!);
            }
            TakeKeyFaultWithAccess();
        }
    }

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                if (!released)
                    InheritOperationFault(source.OperationFault);
        }
    }

    void RebuildWithAccess()
    {
        keys.Clear();
        var elementsByKey = new Dictionary<TKey, List<TElement>>(KeyEqualityComparer);
        elementsByKeyBeingRebuilt = elementsByKey;
        try
        {
            ObserveSourceWithAccess();
        }
        finally
        {
            elementsByKeyBeingRebuilt = null;
        }
        foreach (var (key, collectionAndGrouping) in collectionAndGroupingByKey.ToList())
        {
            if (!elementsByKey.TryGetValue(key, out var retained))
                retained = [];
            collectionAndGrouping.collection.Reset(retained);
        }
        foreach (var (key, keyElements) in elementsByKey)
            if (!collectionAndGroupingByKey.ContainsKey(key))
                foreach (var element in keyElements)
                    AddElement(element, key);
    }

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Remove(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    bool IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Remove(TKey key) =>
        throw new NotSupportedException();

    void RemoveElement(TElement element, TKey key, bool isOnlyOccurrence)
    {
        if (collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            var collection = collectionAndGrouping.collection;
            collection.RemoveInstance(element, isOnlyOccurrence);
        }
    }

    void TakeKeyFaultWithAccess()
    {
        if (keys.TryTakeFault(out var fault))
            OperationFault = fault;
    }

    public override string ToString() =>
        $"grouping of {source} by {KeySelector}";

    public bool TryGetValue(TKey key, out IObservableGrouping<TKey, TElement> value)
    {
        lock (access)
            return groupingByKey.TryGetValue(key, out value);
    }
}
