namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionarySelectQuery<TKey, TValue, TSourceKey, TSourceValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TSourceKey, TSourceValue> source, Expression<Func<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> keyValuePairSelector, IEqualityComparer<TKey> equalityComparer) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
    where TSourceKey : notnull
{
    readonly object access = new();
    readonly Dictionary<TKey, List<TSourceKey>> claimantsByProjectedKey = new(equalityComparer);
    int duplicateClaims;
    int nullKeys;
    readonly ObservableDictionary<TSourceKey, (IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> ObservableExpression, Exception? CommittedFault, KeyValuePair<TKey, TValue> CommittedProjection)> observableExpressions = [];
    readonly ObservableDictionary<TKey, TValue> result = new(equalityComparer);
    readonly IEqualityComparer<TSourceKey> sourceKeyComparer = EqualityComparer<TSourceKey>.Default;
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

    public override int Count
    {
        get
        {
            lock (access)
                return result.Count;
        }
    }

    public override IEnumerable<TKey> Keys
    {
        get
        {
            lock (access)
                return result.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock (access)
                return result.Values.ToList().AsReadOnly();
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
        if (claimantsByProjectedKey.TryGetValue(key, out var claimants))
        {
            claimants.Add(sourceKey);
            ++duplicateClaims;
        }
        else
        {
            claimantsByProjectedKey.Add(key, [sourceKey]);
            into.Add(key, projection.Value);
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
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                source.DictionaryChanged -= SourceDictionaryChanged;
                result.CollectionChanged -= ResultCollectionChanged;
                ((INotifyDictionaryChanged)result).DictionaryChanged -= ResultDictionaryChangedBoxed;
                result.DictionaryChanged -= ResultDictionaryChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (access)
            return result.ToList().GetEnumerator();
    }

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return result.GetRange(keys);
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            var sourceKey = observableExpression.Argument.Key;
            if (!observableExpressions.TryGetValue(sourceKey, out var committed) || !ReferenceEquals(committed.ObservableExpression, observableExpression))
                return;
            var (newFault, newProjection) = observableExpression.Evaluation;
            var committedProjection = committed.CommittedProjection;
            var committedKey = committedProjection.Key;
            var newKey = newProjection.Key;
            var keyIsUnchanged = committedKey is null ? newKey is null : newKey is not null && EqualityComparer.Equals(committedKey, newKey);
            if (keyIsUnchanged)
            {
                observableExpressions[sourceKey] = (observableExpression, newFault, newProjection);
                if (newKey is not null && !valueEqualityComparer.Equals(committedProjection.Value, newProjection.Value) && IsFirstClaimantWithAccess(newKey, sourceKey))
                    result[newKey] = newProjection.Value;
            }
            else
            {
                RetractProjectionWithAccess(sourceKey, committedProjection);
                observableExpressions[sourceKey] = (observableExpression, newFault, newProjection);
                ApplyProjectionWithAccess(sourceKey, newProjection, result);
            }
            if (!keyIsUnchanged || !ReferenceEquals(committed.CommittedFault, newFault))
                SetOperationFault();
        }
    }

    bool IsFirstClaimantWithAccess(TKey key, TSourceKey sourceKey) =>
        claimantsByProjectedKey.TryGetValue(key, out var claimants) && claimants.Count > 0 && sourceKeyComparer.Equals(claimants[0], sourceKey);

    void ObserveSourceKeyValuePairWithAccess(KeyValuePair<TSourceKey, TSourceValue> sourceKeyValuePair, ObservableDictionary<TKey, TValue> into)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(KeyValuePairSelector, sourceKeyValuePair);
        var (fault, projection) = observableExpression.Evaluation;
        ApplyProjectionWithAccess(sourceKeyValuePair.Key, projection, into);
        observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        observableExpressions.Add(sourceKeyValuePair.Key, (observableExpression, fault, projection));
    }

    protected override void OnInitialization()
    {
        foreach (var sourceKeyValuePair in source)
            ObserveSourceKeyValuePairWithAccess(sourceKeyValuePair, result);
        SetOperationFault();
        source.DictionaryChanged += SourceDictionaryChanged;
        result.CollectionChanged += ResultCollectionChanged;
        ((INotifyDictionaryChanged)result).DictionaryChanged += ResultDictionaryChangedBoxed;
        result.DictionaryChanged += ResultDictionaryChanged;
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
        var claimantIndex = claimants.FindIndex(claimant => sourceKeyComparer.Equals(claimant, sourceKey));
        if (claimantIndex < 0)
            return;
        claimants.RemoveAt(claimantIndex);
        if (claimants.Count == 0)
        {
            claimantsByProjectedKey.Remove(key);
            result.Remove(key);
            return;
        }
        --duplicateClaims;
        if (claimantIndex == 0 && observableExpressions.TryGetValue(claimants[0], out var promoted))
            result[key] = promoted.CommittedProjection.Value;
    }

    void SetOperationFault()
    {
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

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TSourceKey, TSourceValue> e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (e.Action is NotifyDictionaryChangedAction.Reset)
            {
                claimantsByProjectedKey.Clear();
                duplicateClaims = 0;
                nullKeys = 0;
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();

                var newResult = new ObservableDictionary<TKey, TValue>(EqualityComparer);
                foreach (var sourceKeyValuePair in source)
                    ObserveSourceKeyValuePairWithAccess(sourceKeyValuePair, newResult);
                result.Reset(newResult);

                claimantsByProjectedKey.TrimExcess();
                observableExpressions.TrimExcess();
            }
            else
            {
                if (e.OldItems is { } oldItems)
                    foreach (var keyValuePair in oldItems)
                    {
                        if (!observableExpressions.TryGetValue(keyValuePair.Key!, out var committed))
                            continue;
                        RetractProjectionWithAccess(keyValuePair.Key!, committed.CommittedProjection);
                        committed.ObservableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                        committed.ObservableExpression.Dispose();
                        observableExpressions.Remove(keyValuePair.Key!);
                    }
                if (e.NewItems is { } newItems)
                    foreach (var keyValuePair in newItems)
                        ObserveSourceKeyValuePairWithAccess(keyValuePair, result);
            }
            SetOperationFault();
        }
    }

    public override string ToString() =>
        $"mapping {source} with {KeyValuePairSelector} and using {EqualityComparer}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return result.TryGetValue(key, out value);
    }
}
