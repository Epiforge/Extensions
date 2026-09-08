namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryWhereQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, Expression<Func<KeyValuePair<TKey, TValue>, bool>> predicate) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    readonly object access = new();
    IReadOnlyList<KeyValuePair<TKey, TValue>>? enumerationSnapshot;
    IReadOnlyList<TKey>? keysSnapshot;
    PropertyChangedEventHandler? observableExpressionPropertyChangedHandler;
    readonly Dictionary<TKey, (IObservableExpression<KeyValuePair<TKey, TValue>, bool> ObservableExpression, Exception? CommittedFault, bool IsIncluded)> observableExpressions = new(source.KeyComparer);
    readonly ObservableDictionary<TKey, TValue> result = new(source.KeyComparer);
    IReadOnlyList<TValue>? valuesSnapshot;
    internal readonly Expression<Func<KeyValuePair<TKey, TValue>, bool>> Predicate = predicate;

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

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock (access)
                return valuesSnapshot ??= result.Values.ToList().AsReadOnly();
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
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                    observableExpression.Dispose();
                }
                source.DictionaryChanged -= SourceDictionaryChanged;
                result.CollectionChanged -= ResultCollectionChanged;
                ((INotifyDictionaryChanged)result).DictionaryChanged -= ResultDictionaryChangedBoxed;
                result.DictionaryChanged -= ResultDictionaryChanged;
                result.PropertyChanging -= ResultPropertyChanging;
                result.PropertyChanged -= ResultPropertyChanged;
                RemovedFromCache();
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
        var faultList = new FaultList();
        var expressionObserver = collectionObserver.ExpressionObserver;
        foreach (var keyValuePair in source)
        {
            var observableExpression = expressionObserver.ObserveWithoutOptimization(Predicate, keyValuePair);
            var (fault, predicateResult) = observableExpression.Evaluation;
            var isIncluded = fault is null && predicateResult;
            if (!faultList.Check(observableExpression) && isIncluded)
                AddToResultWithAccess(keyValuePair.Key, keyValuePair.Value);
            observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
            observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, isIncluded));
        }
        OperationFault = faultList.Fault;
        source.DictionaryChanged += SourceDictionaryChanged;
        result.PropertyChanging += ResultPropertyChanging;
        result.PropertyChanged += ResultPropertyChanged;
    }

    void AddToResultWithAccess(TKey key, TValue value)
    {
        DiscardSnapshots();
        result.Add(key, value);
    }

    void DiscardSnapshots()
    {
        enumerationSnapshot = null;
        keysSnapshot = null;
        valuesSnapshot = null;
    }

    void RemoveFromResultWithAccess(TKey key)
    {
        DiscardSnapshots();
        result.Remove(key);
    }

    void ResetResultWithAccess(ObservableDictionary<TKey, TValue> newResult)
    {
        DiscardSnapshots();
        result.Reset(newResult);
    }

    void ResultCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ResultDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void ResultDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    void ResultPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ResultPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    /// <summary>
    /// Yields the one handler this query attaches to every observation it makes, since a method group converts to a new delegate at each conversion and this one is converted twice for every element
    /// </summary>
    PropertyChangedEventHandler ObservableExpressionPropertyChangedHandler =>
        observableExpressionPropertyChangedHandler ??= ObservableExpressionPropertyChanged;

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<KeyValuePair<TKey, TValue>, bool> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            var keyValuePair = observableExpression.Argument;
            var key = keyValuePair.Key;
            if (!observableExpressions.TryGetValue(key, out var committed) || !ReferenceEquals(committed.ObservableExpression, observableExpression))
                return;
            var (newFault, newPredicateResult) = observableExpression.Evaluation;
            var isIncluded = newFault is null && newPredicateResult;
            if (!committed.IsIncluded && isIncluded)
                AddToResultWithAccess(key, keyValuePair.Value);
            else if (committed.IsIncluded && !isIncluded)
                RemoveFromResultWithAccess(key);
            observableExpressions[key] = (observableExpression, newFault, isIncluded);
            if (FaultList.ExchangeKeyFault(OperationFault, key, source.KeyComparer, committed.CommittedFault, newFault, out var newOperationFault))
                OperationFault = newOperationFault;
        }
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            var expressionObserver = collectionObserver.ExpressionObserver;
            if (e.Action is NotifyDictionaryChangedAction.Reset)
            {
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();
                var newResult = new ObservableDictionary<TKey, TValue>(source.KeyComparer);
                var faultList = new FaultList();
                foreach (var keyValuePair in source)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Predicate, keyValuePair);
                    var (fault, predicateResult) = observableExpression.Evaluation;
                    var isIncluded = fault is null && predicateResult;
                    if (!faultList.Check(observableExpression) && isIncluded)
                        newResult.Add(keyValuePair.Key, keyValuePair.Value);
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
                    observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, isIncluded));
                }
                ResetResultWithAccess(newResult);
                OperationFault = faultList.Fault;
            }
            else
            {
                FaultList? faultList = null;
                foreach (var keyValuePair in e.OldItems)
                {
                    var key = keyValuePair.Key;
                    if (!observableExpressions.TryGetValue(key, out var committed))
                        continue;
                    if (committed.CommittedFault is not null)
                    {
                        faultList ??= new FaultList(OperationFault);
                        faultList.RemoveKey(key, source.KeyComparer);
                    }
                    else if (committed.IsIncluded)
                        RemoveFromResultWithAccess(key);
                    committed.ObservableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                    committed.ObservableExpression.Dispose();
                    observableExpressions.Remove(key);
                }
                foreach (var keyValuePair in e.NewItems)
                {
                    var key = keyValuePair.Key;
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Predicate, keyValuePair);
                    var (fault, predicateResult) = observableExpression.Evaluation;
                    var isIncluded = fault is null && predicateResult;
                    if (fault is not null)
                    {
                        faultList ??= new FaultList(OperationFault);
                        faultList.Check(observableExpression);
                    }
                    else if (isIncluded)
                        AddToResultWithAccess(key, keyValuePair.Value);
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
                    observableExpressions.Add(key, (observableExpression, fault, isIncluded));
                }
                if (faultList is not null)
                    OperationFault = faultList.Fault;
            }
        }
    }

    public override string ToString() =>
        $"{source} matching {Predicate}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return result.TryGetValue(key, out value);
    }
}
