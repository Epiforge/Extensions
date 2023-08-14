namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionarySelectQuery<TKey, TValue, TSourceKey, TSourceValue> :
    ObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
    where TSourceKey : notnull
{
    public ObservableDictionarySelectQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TSourceKey, TSourceValue> source, Expression<Func<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> keyValuePairSelector, IEqualityComparer<TKey> equalityComparer) :
        base(collectionObserver)
    {
        access = new();
        duplicateKeys = new();
        evaluationsChanging = new();
        observableExpressions = new();
        result = new(equalityComparer);
        this.source = source;
        valueEqualityComparer = EqualityComparer<TValue>.Default;
        KeyValuePairSelector = keyValuePairSelector;
        EqualityComparer = equalityComparer;
    }

    readonly object access;
    readonly ObservableDictionary<TKey, int> duplicateKeys;
    readonly Dictionary<IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>, (Exception? fault, KeyValuePair<TKey, TValue> result)> evaluationsChanging;
    int nullKeys;
    readonly ObservableDictionary<TSourceKey, IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> observableExpressions;
    readonly ObservableDictionary<TKey, TValue> result;
    readonly ObservableDictionaryQuery<TSourceKey, TSourceValue> source;
    readonly IEqualityComparer<TValue> valueEqualityComparer;

    internal readonly IEqualityComparer<TKey> EqualityComparer;
    internal readonly Expression<Func<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>> KeyValuePairSelector;

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

    public override bool ContainsKey(TKey key)
    {
        lock (access)
            return result.ContainsKey(key);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                foreach (var observableExpression in observableExpressions.Values)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                source.DictionaryChanged -= SourceDictionaryChanged;
                result.DictionaryChanged -= ResultDictionaryChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (access)
            foreach (var keyValuePair in result)
                yield return keyValuePair;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> observableExpression && e.PropertyName == nameof(IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>.Evaluation))
            lock (access)
            {
                var (oldFault, oldResult) = evaluationsChanging![observableExpression];
                evaluationsChanging.Remove(observableExpression);
                var (newFault, newResult) = observableExpression.Evaluation;
                var addedFault = oldFault is null && newFault is not null;
                var removedFault = oldFault is not null && newFault is null;
                var replacedFault = oldFault is not null && newFault is not null && !ReferenceEquals(oldFault, newFault);
                if (addedFault || removedFault || replacedFault)
                {
                    var elementFaults = OperationFault is AggregateException aggregateException ? aggregateException.InnerExceptions.OfType<EvaluationFaultException>().ToList() : new List<EvaluationFaultException>();
                    if (removedFault || replacedFault)
                        elementFaults.RemoveAll(elementFault => ReferenceEquals(elementFault.Element, observableExpression.Argument.Key));
                    if (addedFault || replacedFault)
                        elementFaults.Add(new EvaluationFaultException(observableExpression.Argument.Key, newFault!));
                    var faultList = new FaultList();
                    if (OperationFault is AggregateException aggregateException2)
                        faultList.AddRange(aggregateException2.InnerExceptions.Where(innerEx => innerEx is not EvaluationFaultException));
                    else if (OperationFault is { } otherEx && otherEx is not EvaluationFaultException)
                        faultList.Add(otherEx);
                    faultList.AddRange(elementFaults);
                    OperationFault = faultList.Fault;
                }
                if (!valueEqualityComparer.Equals(oldResult.Value, newResult.Value))
                    result[oldResult.Key] = newResult.Value;
            }
    }

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (sender is IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>> observableExpression && e.PropertyName == nameof(IObservableExpression<KeyValuePair<TSourceKey, TSourceValue>, KeyValuePair<TKey, TValue>>.Evaluation))
            lock(access)
                evaluationsChanging.Add(observableExpression, observableExpression.Evaluation);
    }

    protected override void OnInitialization()
    {
        var expressionObserver = collectionObserver.ExpressionObserver;
        foreach (var sourceKeyValuePair in source)
        {
            var observableExpression = expressionObserver.ObserveWithoutOptimization(KeyValuePairSelector, sourceKeyValuePair);
            if (observableExpression.Evaluation.Result is KeyValuePair<TKey, TValue> resultKeyValuePair)
            {
                if (resultKeyValuePair.Key is null)
                    ++nullKeys;
                else if (!result.TryAdd(resultKeyValuePair.Key, resultKeyValuePair.Value))
                {
                    if (!duplicateKeys.TryGetValue(resultKeyValuePair.Key, out var count))
                        duplicateKeys.Add(resultKeyValuePair.Key, 2);
                    else
                        duplicateKeys[resultKeyValuePair.Key] = count + 1;
                }
            }
            else
                ++nullKeys;
            observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            observableExpressions.Add(sourceKeyValuePair.Key, observableExpression);
        }
        SetOperationFault();
        source.DictionaryChanged += SourceDictionaryChanged;
        result.DictionaryChanged += ResultDictionaryChanged;
    }

    void ResultDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void SetOperationFault()
    {
        var faultList = new FaultList();
        if (nullKeys > 0)
            faultList.AddRange(Enumerable.Range(0, nullKeys).Select(_ => ExceptionHelper.KeyNull));
        if (duplicateKeys.Count > 0)
            faultList.AddRange(Enumerable.Range(0, duplicateKeys.Keys.Sum(key => duplicateKeys[key] - 1)).Select(_ => ExceptionHelper.SameKeyAlreadyAdded));
        faultList.Check(source!);
        foreach (var observableExpression in observableExpressions.Values)
            faultList.Check(observableExpression);
        OperationFault = faultList.Fault;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TSourceKey, TSourceValue> e)
    {
        lock (access)
        {
            var expressionObserver = collectionObserver.ExpressionObserver;
            if (e.Action is NotifyDictionaryChangedAction.Reset)
            {
                duplicateKeys.Clear();
                nullKeys = 0;
                foreach (var observableExpression in observableExpressions.Values)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();

                var newResult = new ObservableDictionary<TKey, TValue>();
                foreach (var sourceKeyValuePair in source)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(KeyValuePairSelector, sourceKeyValuePair);
                    if (observableExpression.Evaluation.Result is KeyValuePair<TKey, TValue> resultKeyValuePair)
                    {
                        if (resultKeyValuePair.Key is null)
                            ++nullKeys;
                        else if (!newResult.TryAdd(resultKeyValuePair.Key, resultKeyValuePair.Value))
                        {
                            if (!duplicateKeys.TryGetValue(resultKeyValuePair.Key, out var count))
                                duplicateKeys.Add(resultKeyValuePair.Key, 2);
                            else
                                duplicateKeys[resultKeyValuePair.Key] = count + 1;
                        }
                    }
                    else
                        ++nullKeys;
                    observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(sourceKeyValuePair.Key, observableExpression);
                }
                result.Reset(newResult);

                duplicateKeys.TrimExcess();
                observableExpressions.TrimExcess();
                SetOperationFault();
            }
            else
            {
                if (e.OldItems is { } oldItems)
                    foreach (var keyValuePair in oldItems)
                    {
                        var observableExpression = observableExpressions[keyValuePair.Key!];
                        var key = observableExpression.Evaluation.Result.Key;
                        observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                        observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                        observableExpression.Dispose();
                        observableExpressions.Remove(keyValuePair.Key!);
                        if (key is null)
                            --nullKeys;
                        else if (duplicateKeys.TryGetValue(key, out var count))
                        {
                            if (count == 2)
                                duplicateKeys.Remove(key);
                            else
                                duplicateKeys[key] = count - 1;
                        }
                        else
                            result.Remove(key);
                    }
                if (e.NewItems is { } newItems)
                    foreach (var keyValuePair in newItems)
                    {
                        var observableExpression = expressionObserver.ObserveWithoutOptimization(KeyValuePairSelector, keyValuePair);
                        if (observableExpression.Evaluation.Result is KeyValuePair<TKey, TValue> resultKeyValuePair)
                        {
                            if (resultKeyValuePair.Key is null)
                                ++nullKeys;
                            else if (!result.TryAdd(resultKeyValuePair.Key, resultKeyValuePair.Value))
                            {
                                if (!duplicateKeys.TryGetValue(resultKeyValuePair.Key, out var count))
                                    duplicateKeys.Add(resultKeyValuePair.Key, 2);
                                else
                                    duplicateKeys[resultKeyValuePair.Key] = count + 1;
                            }
                        }
                        else
                            ++nullKeys;
                        observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                        observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                        observableExpressions.Add(keyValuePair.Key!, observableExpression);
                    }
            }
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
