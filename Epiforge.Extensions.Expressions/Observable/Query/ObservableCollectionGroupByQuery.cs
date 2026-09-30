namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionGroupByQuery<TKey, TElement> :
    ObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IObservableQueryDependent,
    ObservedElementKeys<TElement, TKey>.IOwner
{
    public ObservableCollectionGroupByQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) :
        base(collectionObserver)
    {
        this.source = source;
        KeySelector = keySelector;
        KeyEqualityComparer = keyEqualityComparer;
        access = new();
        collectionAndGroupingByKey = new(KeyEqualityComparer);
        groupings = [];
        keys = new(this, collectionObserver.ExpressionObserver, keySelector);
    }

    readonly object access;
    IReadOnlyList<IObservableGrouping<TKey, TElement>>? enumerationSnapshot;
    readonly NullableKeyDictionary<TKey, (GroupCollection<TElement> collection, IObservableGrouping<TKey, TElement> grouping)> collectionAndGroupingByKey;
    NullableKeyDictionary<TKey, List<TElement>>? elementsByKeyBeingRebuilt;
    readonly ObservableRangeCollection<IObservableGrouping<TKey, TElement>> groupings;
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

    public override int Count
    {
        get
        {
            lock (access)
                return groupings.Count;
        }
    }

    /// <summary>
    /// Yields the one handler this query attaches to every key it observes, since a method group converts to a new delegate at each conversion
    /// </summary>
    PropertyChangedEventHandler ObservedElementKeys<TElement, TKey>.IOwner.KeyEvaluationChangedHandler =>
        keyEvaluationChangedHandler ??= KeyEvaluationChanged;

    void AddElement(TElement element, TKey key)
    {
        GroupCollection<TElement> collection;
        if (!collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            collection = collectionObserver.ExpressionObserver.Logger is { } logger ? new(logger) : new();
            var grouping = new ObservableGrouping<TKey, TElement>(collectionObserver, key, collectionObserver.GetObservableCollectionQuery(collection, this), this);
            grouping.InheritOwnerFault(OperationFault);
            grouping.Initialize();
            collectionAndGrouping = (collection, grouping);
            collectionAndGroupingByKey.Add(key, collectionAndGrouping);
            groupings.Add(grouping);
        }
        else
            collection = collectionAndGrouping.collection;
        collection.Add(element);
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

    private protected override void OnOperationFaultPublished(Exception? fault)
    {
        foreach (var grouping in groupings.ToArray())
            ((ObservableGrouping<TKey, TElement>)grouping).InheritOwnerFault(fault);
    }

    void RebuildWithAccess()
    {
        keys.Clear();
        var elementsByKey = new NullableKeyDictionary<TKey, List<TElement>>(KeyEqualityComparer);
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
            if (elementsByKey.TryGetValue(key, out var retained))
                collectionAndGrouping.collection.Reset(retained);
            else
            {
                collectionAndGroupingByKey.Remove(key);
                groupings.Remove(collectionAndGrouping.grouping);
                ((ObservableGrouping<TKey, TElement>)collectionAndGrouping.grouping).InternalDispose();
            }
        foreach (var (key, keyElements) in elementsByKey)
            if (!collectionAndGroupingByKey.ContainsKey(key))
                foreach (var element in keyElements)
                    AddElement(element, key);
    }

    void RemoveElement(TElement element, TKey key, bool isOnlyOccurrence)
    {
        if (collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            var collection = collectionAndGrouping.collection;
            collection.RemoveInstance(element, isOnlyOccurrence);
            if (collection.Count == 0)
            {
                collectionAndGroupingByKey.Remove(key);
                var grouping = collectionAndGrouping.grouping;
                groupings.Remove(grouping);
                ((ObservableGrouping<TKey, TElement>)grouping).InternalDispose();
            }
        }
    }

    void TakeKeyFaultWithAccess()
    {
        if (keys.TryTakeFault(out var fault))
            OperationFault = fault;
    }

    public override string ToString() =>
        $"grouping of {source} by {KeySelector}";
}
