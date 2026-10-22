namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionarySelectQuery<TKey, TValue, TSourceKey, TSourceValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TSourceKey, TSourceValue> source, Expression<Func<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> keyValuePairSelector, IEqualityComparer<TKey> equalityComparer) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
    where TSourceKey : notnull
{
    readonly object access = new();
    IReadOnlyList<KeyValuePair<TKey, TValue>>? enumerationSnapshot;
    IReadOnlyList<TKey>? keysSnapshot;
    readonly Dictionary<TKey, List<TSourceKey>> claimantsByProjectedKey = new(equalityComparer);
    int duplicateClaims;
    bool faultsChanged = true;
    /// <summary>
    /// Whether this query is following a change of its source, during which it delivers a notification for each key it changes, so that a change its subscribers make to the source in the meantime waits until this one has been followed
    /// </summary>
    bool followingSourceChange;
    int nullKeys;
    PropertyChangedEventHandler? observableExpressionPropertyChangedHandler;
    readonly Dictionary<TSourceKey, (IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> ObservableExpression, Exception? CommittedFault, KeyValuePair<TKey, TValue> CommittedProjection)> observableExpressions = new(source.KeyComparer);
    /// <summary>
    /// The changes the source announced while this query was following another, in the order they were announced, made with the first of them
    /// </summary>
    Queue<NotifyDictionaryChangedEventArgs<TSourceKey, TSourceValue>>? pendingSourceChanges;
    bool released;
    int reportedDuplicateClaims;
    int reportedNullKeys;
    readonly ObservableDictionary<TKey, TValue> result = new(equalityComparer);
    IReadOnlyList<TValue>? valuesSnapshot;
    readonly EqualityComparer<TValue> valueEqualityComparer = EqualityComparer<TValue>.Default;

    internal readonly IEqualityComparer<TKey> EqualityComparer = equalityComparer;
    internal readonly Expression<Func<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> KeyValuePairSelector = keyValuePairSelector;

    public override TValue this[TKey key]
    {
        get
        {
            lock (access)
                return result[key];
        }
    }

    private protected override int CountAtInitialization =>
        result.Count;

    private protected override int CurrentCount
    {
        get
        {
            lock (access)
                return result.Count;
        }
    }

    internal override IEqualityComparer<TKey> KeyComparer =>
        EqualityComparer;

    public override IEnumerable<TKey> Keys
    {
        get
        {
            lock (access)
                return keysSnapshot ??= result.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock (access)
                return valuesSnapshot ??= result.Values.ToList().AsReadOnly();
        }
    }

    void ApplyProjectionWithAccess(TSourceKey sourceKey, KeyValuePair<TKey, TValue> projection, ObservableDictionary<TKey, TValue> into)
    {
        var key = projection.Key;
        if (key is null)
        {
            ++nullKeys;
            return;
        }
        ref var claimants = ref CollectionsMarshal.GetValueRefOrAddDefault(claimantsByProjectedKey, key, out var claimed);
        if (claimed)
        {
            claimants!.Add(sourceKey);
            ++duplicateClaims;
        }
        else
        {
            claimants = [sourceKey];
            DiscardSnapshots();
            into.Add(key, projection.Value);
            if (ReferenceEquals(into, result))
                AnnounceCountOfUnobservedChange();
        }
    }

    public override bool Contains(KeyValuePair<TKey, TValue> item)
    {
        lock (access)
            return result.Contains(item);
    }

    public override bool ContainsKey(TKey key)
    {
        lock (access)
            return result.ContainsKey(key);
    }

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (access)
            ((ICollection<KeyValuePair<TKey, TValue>>)result).CopyTo(array, arrayIndex);
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
                    foreach (var (observableExpression, _, _) in observableExpressions.Values)
                    {
                        observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                        observableExpression.Dispose();
                    }
                    source.DictionaryChanged -= SourceDictionaryChanged;
                    source.PropertyChanged -= SourcePropertyChanged;
                    result.CollectionChanged -= ResultCollectionChanged;
                    ((INotifyDictionaryChanged)result).DictionaryChanged -= ResultDictionaryChangedBoxed;
                    result.DictionaryChanged -= ResultDictionaryChanged;
                }
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (access)
            return (enumerationSnapshot ??= result.ToList().AsReadOnly()).GetEnumerator();
    }

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return result.GetRange(keys);
    }

    /// <summary>
    /// Yields the one handler this query attaches to every observation it makes, since a method group converts to a new delegate at each conversion and this one is converted twice for every element
    /// </summary>
    PropertyChangedEventHandler ObservableExpressionPropertyChangedHandler =>
        observableExpressionPropertyChangedHandler ??= ObservableExpressionPropertyChanged;

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (released)
                return;
            var sourceKey = observableExpression.Argument.Key;
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(observableExpressions, sourceKey);
            if (Unsafe.IsNullRef(ref entry) || !ReferenceEquals(entry.ObservableExpression, observableExpression))
                return;
            var committed = entry;
            var (newFault, newProjection) = observableExpression.Evaluation;
            entry = (observableExpression, newFault, newProjection);
            var committedProjection = committed.CommittedProjection;
            var committedKey = committedProjection.Key;
            var newKey = newProjection.Key;
            var wasApplied = committed.CommittedFault is null;
            var isApplied = newFault is null;
            var keyIsUnchanged = committedKey is null ? newKey is null : newKey is not null && EqualityComparer.Equals(committedKey, newKey);
            var placementIsUnchanged = wasApplied == isApplied && keyIsUnchanged;
            if (placementIsUnchanged)
            {
                if (isApplied && newKey is not null && !valueEqualityComparer.Equals(committedProjection.Value, newProjection.Value) && IsFirstClaimantWithAccess(newKey, sourceKey))
                    SetInResultWithAccess(newKey, newProjection.Value);
            }
            else
            {
                if (wasApplied)
                    RetractProjectionWithAccess(sourceKey, committedProjection);
                if (isApplied)
                    ApplyProjectionWithAccess(sourceKey, newProjection, result);
            }
            if (!ReferenceEquals(committed.CommittedFault, newFault))
                faultsChanged = true;
            SetOperationFault();
        }
    }

    bool IsFirstClaimantWithAccess(TKey key, TSourceKey sourceKey) =>
        claimantsByProjectedKey.TryGetValue(key, out var claimants) && claimants.Count > 0 && source.KeyComparer.Equals(claimants[0], sourceKey);

    void ObserveSourceKeyValuePairWithAccess(KeyValuePair<TSourceKey, TSourceValue> sourceKeyValuePair, ObservableDictionary<TKey, TValue> into)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(KeyValuePairSelector, sourceKeyValuePair);
        observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
        var (fault, projection) = observableExpression.Evaluation;
        if (fault is null)
            ApplyProjectionWithAccess(sourceKeyValuePair.Key, projection, into);
        else
            faultsChanged = true;
        observableExpressions.Add(sourceKeyValuePair.Key, (observableExpression, fault, projection));
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
            foreach (var sourceKeyValuePair in source)
                ObserveSourceKeyValuePairWithAccess(sourceKeyValuePair, result);
            SetOperationFault();
            source.DictionaryChanged += SourceDictionaryChanged;
            source.PropertyChanged += SourcePropertyChanged;
        }
    }

    void DiscardSnapshots()
    {
        enumerationSnapshot = null;
        keysSnapshot = null;
        valuesSnapshot = null;
    }

    void ResultCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ResultDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void ResultDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    void RetractProjectionWithAccess(TSourceKey sourceKey, KeyValuePair<TKey, TValue> projection)
    {
        var key = projection.Key;
        if (key is null)
        {
            --nullKeys;
            return;
        }
        if (!claimantsByProjectedKey.TryGetValue(key, out var claimants))
            return;
        var sourceKeyComparer = source.KeyComparer;
        var claimantSpan = CollectionsMarshal.AsSpan(claimants);
        var claimantIndex = 0;
        while (claimantIndex < claimantSpan.Length && !sourceKeyComparer.Equals(claimantSpan[claimantIndex], sourceKey))
            ++claimantIndex;
        if (claimantIndex == claimantSpan.Length)
            return;
        claimants.RemoveAt(claimantIndex);
        if (claimants.Count == 0)
        {
            claimantsByProjectedKey.Remove(key);
            DiscardSnapshots();
            result.Remove(key);
            AnnounceCountOfUnobservedChange();
            return;
        }
        --duplicateClaims;
        if (claimantIndex == 0 && observableExpressions.TryGetValue(claimants[0], out var promoted))
            SetInResultWithAccess(key, promoted.CommittedProjection.Value);
    }

    void SetInResultWithAccess(TKey key, TValue value)
    {
        DiscardSnapshots();
        result[key] = value;
    }

    /// <summary>
    /// Rebuilds the operation fault when what it describes has changed, which is only when a key's fault changed or the counts of null and duplicate keys moved, since rebuilding it otherwise would report a fault standing for the same reasons as a new one
    /// </summary>
    void SetOperationFault()
    {
        if (!faultsChanged && nullKeys == reportedNullKeys && duplicateClaims == reportedDuplicateClaims)
            return;
        faultsChanged = false;
        reportedNullKeys = nullKeys;
        reportedDuplicateClaims = duplicateClaims;
        var faultList = new FaultList();
        if (nullKeys > 0)
            faultList.AddRange(Enumerable.Range(0, nullKeys).Select(_ => ExceptionHelper.KeyNull));
        if (duplicateClaims > 0)
            faultList.AddRange(Enumerable.Range(0, duplicateClaims).Select(_ => ExceptionHelper.SameKeyAlreadyAdded));
        faultList.Check(source!);
        foreach (var (observableExpression, _, _) in observableExpressions.Values)
            faultList.Check(observableExpression);
        OperationFault = faultList.Fault;
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
            {
                faultsChanged = true;
                SetOperationFault();
            }
        }
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TSourceKey, TSourceValue> e)
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

    /// <summary>
    /// Follows a change of the source one key at a time, delivering the notification of each key before taking the next, so that a subscriber finds this query as each notification leaves it
    /// </summary>
    void FollowSourceChange(NotifyDictionaryChangedEventArgs<TSourceKey, TSourceValue> e)
    {
        if (e.Action is NotifyDictionaryChangedAction.Reset)
        {
            lock (access)
            {
                if (released)
                    return;
                faultsChanged = true;
                claimantsByProjectedKey.Clear();
                duplicateClaims = 0;
                nullKeys = 0;
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();

                var newResult = new ObservableDictionary<TKey, TValue>(EqualityComparer);
                foreach (var sourceKeyValuePair in source)
                    ObserveSourceKeyValuePairWithAccess(sourceKeyValuePair, newResult);
                DiscardSnapshots();
                result.Reset(newResult);
                AnnounceCountOfUnobservedChange();

                claimantsByProjectedKey.TrimExcess();
                observableExpressions.TrimExcess();
                SetOperationFault();
            }
            return;
        }
        if (e.OldItems is { } oldItems)
            foreach (var keyValuePair in oldItems)
            {
                lock (access)
                {
                    if (released)
                        return;
                    if (!observableExpressions.Remove(keyValuePair.Key!, out var committed))
                        continue;
                    if (committed.CommittedFault is null)
                        RetractProjectionWithAccess(keyValuePair.Key!, committed.CommittedProjection);
                    else
                        faultsChanged = true;
                    committed.ObservableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                    committed.ObservableExpression.Dispose();
                }
                RaiseNotificationsDeferredSoFar();
            }
        if (e.NewItems is { } newItems)
            foreach (var keyValuePair in newItems)
            {
                lock (access)
                {
                    if (released)
                        return;
                    ObserveSourceKeyValuePairWithAccess(keyValuePair, result);
                }
                RaiseNotificationsDeferredSoFar();
            }
        lock (access)
            if (!released)
                SetOperationFault();
    }

    public override string ToString() =>
        $"mapping {source} with {KeyValuePairSelector} and using {EqualityComparer}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return result.TryGetValue(key, out value);
    }
}
