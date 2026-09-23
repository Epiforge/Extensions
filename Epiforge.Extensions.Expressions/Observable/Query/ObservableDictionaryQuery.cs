namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableDictionaryQuery<TKey, TValue>(CollectionObserver collectionObserver) :
    ObservableQuery(collectionObserver),
    IObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Distinguishes deferred boxed dictionary change notifications from unboxed ones, whose runtime types coincide when the key and value are both <see cref="object"/>
    /// </summary>
    sealed class DeferredDictionaryChangedBoxed(NotifyDictionaryChangedEventArgs<object?, object?> eventArguments)
    {
        internal readonly NotifyDictionaryChangedEventArgs<object?, object?> EventArguments = eventArguments;
    }

    #region Cache Comparers

    class CachedSelectQueryEqualityComparer :
        IEqualityComparer<(Expression keyValuePairSelector, object equalityComparer)>
    {
        public static CachedSelectQueryEqualityComparer Default { get; } = new();

        public bool Equals((Expression keyValuePairSelector, object equalityComparer) x, (Expression keyValuePairSelector, object equalityComparer) y)
        {
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            return expressionEqualityComparer.Equals(x.keyValuePairSelector, y.keyValuePairSelector) && ReferenceEquals(x.equalityComparer, y.equalityComparer);
        }

        public int GetHashCode([DisallowNull] (Expression keyValuePairSelector, object equalityComparer) obj)
        {
            var hashCode = new System.HashCode();
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.keyValuePairSelector));
            hashCode.Add(obj.equalityComparer);
            return hashCode.ToHashCode();
        }
    }

    #endregion Cache Comparers

    /// <summary>
    /// Holds the lambdas a cast observes, built once for each pair of types, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class CastLambdas<TResult>
    {
        internal static readonly Expression<Func<TKey, TValue, bool>> IsResult = (key, value) => value is TResult;
        internal static readonly Expression<Func<TKey, TValue, TKey>> Key = (key, value) => key;
        internal static readonly Expression<Func<TKey, TValue, TResult>> Value = (key, value) => (TResult)(object)value!;
    }

    /// <summary>
    /// Holds the lambda over a key-value pair observed in place of a caller's predicate, built once for each predicate and kept for as long as its caller keeps the predicate, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class PairPredicates
    {
        static readonly ConditionalWeakTable<Expression<Func<TKey, TValue, bool>>, Expression<Func<KeyValuePair<TKey, TValue>, bool>>> byPredicate = [];

        internal static Expression<Func<KeyValuePair<TKey, TValue>, bool>> For(Expression<Func<TKey, TValue, bool>> predicate) =>
            byPredicate.GetValue(predicate, static source =>
            {
                var keyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
                var keyExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<,>.Key));
                var valueExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<,>.Value));
                var predicateExpression = LambdaInvocationRewriter.Apply(source, keyExpression, valueExpression) ?? Expression.Invoke(source, keyExpression, valueExpression);
                return Expression.Lambda<Func<KeyValuePair<TKey, TValue>, bool>>(predicateExpression, keyValuePairParameter);
            });
    }

    /// <summary>
    /// Holds the lambda over a key-value pair observed in place of a caller's key and value selectors, built once for each pair of selectors and kept for as long as its caller keeps both, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class PairProjections<TResultKey, TResultValue>
    {
        static readonly ConditionalWeakTable<Expression<Func<TKey, TValue, TResultKey>>, ConditionalWeakTable<Expression<Func<TKey, TValue, TResultValue>>, Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>>> byKeySelector = [];

        static Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>> Add(ConditionalWeakTable<Expression<Func<TKey, TValue, TResultValue>>, Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>> byValueSelector, Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector) =>
            byValueSelector.GetValue(valueSelector, _ =>
            {
                var sourceKeyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
                var sourceKeyExpression = Expression.Property(sourceKeyValuePairParameter, nameof(KeyValuePair<,>.Key));
                var sourceValueExpression = Expression.Property(sourceKeyValuePairParameter, nameof(KeyValuePair<,>.Value));
                var keyExpression = LambdaInvocationRewriter.Apply(keySelector, sourceKeyExpression, sourceValueExpression) ?? Expression.Invoke(keySelector, sourceKeyExpression, sourceValueExpression);
                var valueExpression = LambdaInvocationRewriter.Apply(valueSelector, sourceKeyExpression, sourceValueExpression) ?? Expression.Invoke(valueSelector, sourceKeyExpression, sourceValueExpression);
                var keyValuePairExpression = Expression.New(typeof(KeyValuePair<TResultKey, TResultValue>).GetConstructor([typeof(TResultKey), typeof(TResultValue)])!, keyExpression, valueExpression);
                return Expression.Lambda<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>(keyValuePairExpression, sourceKeyValuePairParameter);
            });

        internal static Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>> For(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector)
        {
            var byValueSelector = byKeySelector.GetValue(keySelector, static _ => new());
            return byValueSelector.TryGetValue(valueSelector, out var projection) ? projection : Add(byValueSelector, keySelector, valueSelector);
        }
    }

    /// <summary>
    /// Holds the lambda over a key-value pair observed in place of a caller's selector, built once for each selector and kept for as long as its caller keeps the selector, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class PairSelectors<TElement>
    {
        static readonly ConditionalWeakTable<Expression<Func<TKey, TValue, TElement>>, Expression<Func<KeyValuePair<TKey, TValue>, TElement>>> bySelector = [];

        internal static Expression<Func<KeyValuePair<TKey, TValue>, TElement>> For(Expression<Func<TKey, TValue, TElement>> selector) =>
            bySelector.GetValue(selector, static source =>
            {
                var keyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
                var keyExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<,>.Key));
                var valueExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<,>.Value));
                var selectorExpression = LambdaInvocationRewriter.Apply(source, keyExpression, valueExpression) ?? Expression.Invoke(source, keyExpression, valueExpression);
                return Expression.Lambda<Func<KeyValuePair<TKey, TValue>, TElement>>(selectorExpression, keyValuePairParameter);
            });
    }

    /// <summary>
    /// Holds the reversal of each comparer a caller seeks the last key by, built once for each comparer and kept for as long as its caller keeps the comparer, since the cache of keyed queries matches comparers by equality and a reversal has none of its own
    /// </summary>
    static class ReversedComparers
    {
        static readonly ConditionalWeakTable<IComparer<TKey>, ReversedComparer<TKey>> byComparer = [];

        internal static ReversedComparer<TKey> For(IComparer<TKey> comparer) =>
            byComparer.GetValue(comparer, static source => new(source));
    }

    /// <summary>
    /// Holds the lambda selecting a value, built once for each pair of key and value types, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class ValueLambdas
    {
        internal static readonly Expression<Func<TKey, TValue, TValue>> Selector = (key, value) => value;
    }

    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery>? cachedAggregateQueries;
    Dictionary<Expression, ObservableDictionaryAllQuery<TKey, TValue>>? cachedAllQueries;
    NullableKeyDictionary<Expression?, ObservableDictionaryAnyQuery<TKey, TValue>>? cachedAnyQueries;
    ObservableDictionaryConcurrentQuery<TKey, TValue>? cachedConcurrentQuery;
    ObservableDictionaryCountQuery<TKey, TValue>? cachedCountQuery;
    Dictionary<(IComparer<TKey>? keyComparer, bool notFoundIsDefault), ObservableDictionaryKeyedQuery<TKey, TValue>>? cachedKeyedQueries;
    Dictionary<(Expression keyValuePairSelector, object equalityComparer), ObservableQuery>? cachedSelectQueries;
    Dictionary<Expression, ObservableQuery>? cachedToCollectionQueries;
    Dictionary<SynchronizationContext, ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue>>? cachedUsingSynchronizationContextEventuallyQueries;
    Dictionary<SynchronizationContext, ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue>>? cachedUsingSynchronizationContextQueries;
    Dictionary<(TKey key, bool notFoundIsDefault), ObservableDictionaryValueForQuery<TKey, TValue>>? cachedValueForQueries;
    Dictionary<Expression<Func<KeyValuePair<TKey, TValue>, bool>>, ObservableQuery>? cachedWhereQueries;
    NotifyCollectionChangedEventHandler? collectionChanged;
    EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? dictionaryChanged;
    EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? dictionaryChangedBoxed;
#if IS_NET_9_0_OR_GREATER
    Lock? cachedQueriesAccess;
    readonly Lock changeAccess = new();
#else
    object? cachedQueriesAccess;
    readonly object changeAccess = new();
#endif
    Exception? operationFault;

    public abstract TValue this[TKey key] { get; }

    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        get => this[key];
        set => throw new NotSupportedException();
    }

    public override int CachedObservableQueries
    {
        get
        {
            if (Volatile.Read(ref cachedQueriesAccess) is not { } access)
                return 0;
            var count = 0;
            lock (access)
            {
                count += cachedAggregateQueries?.Values.Sum(aggregateQuery => 1 + aggregateQuery.CachedObservableQueries) ?? 0;
                count += cachedAllQueries?.Values.Sum(allQuery => 1 + allQuery.CachedObservableQueries) ?? 0;
                count += cachedAnyQueries?.Values.Sum(anyQuery => 1 + anyQuery.CachedObservableQueries) ?? 0;
                count += cachedConcurrentQuery is null ? 0 : 1 + cachedConcurrentQuery.CachedObservableQueries;
                count += cachedCountQuery is null ? 0 : 1 + cachedCountQuery.CachedObservableQueries;
                count += cachedKeyedQueries?.Values.Sum(keyedQuery => 1 + keyedQuery.CachedObservableQueries) ?? 0;
                count += cachedSelectQueries?.Values.Sum(selectQuery => 1 + selectQuery.CachedObservableQueries) ?? 0;
                count += cachedToCollectionQueries?.Values.Sum(toCollectionQuery => 1 + toCollectionQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationContextEventuallyQueries?.Values.Sum(usingSynchronizationContextEventuallyQuery => 1 + usingSynchronizationContextEventuallyQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationContextQueries?.Values.Sum(usingSynchronizationContextQuery => 1 + usingSynchronizationContextQuery.CachedObservableQueries) ?? 0;
                count += cachedValueForQueries?.Values.Sum(valueQuery => 1 + valueQuery.CachedObservableQueries) ?? 0;
                count += cachedWhereQueries?.Values.Sum(whereQuery => 1 + whereQuery.CachedObservableQueries) ?? 0;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets the lock guarding this query's caches of the queries built over it, created by whichever observation first needs it, so that a query nothing is built over allocates none
    /// </summary>
#if IS_NET_9_0_OR_GREATER
    Lock CachedQueriesAccess
#else
    object CachedQueriesAccess
#endif
    {
        get
        {
            if (Volatile.Read(ref cachedQueriesAccess) is { } access)
                return access;
            access = new();
            return Interlocked.CompareExchange(ref cachedQueriesAccess, access, null) ?? access;
        }
    }

    public abstract int Count { get; }

    public bool IsReadOnly =>
        true;

    public virtual bool IsSynchronized =>
        false;

    internal virtual IEqualityComparer<TKey> KeyComparer =>
        EqualityComparer<TKey>.Default;

    public abstract IEnumerable<TKey> Keys { get; }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys =>
        Keys.ToList().AsReadOnly();

    public virtual Exception? OperationFault
    {
        get => operationFault;
        protected set => SetBackedProperty(ref operationFault, in value, operationFaultPropertyChangingEventArgs, operationFaultPropertyChangedEventArgs);
    }

    internal IObservableDictionaryQuery<TKey, TValue> AsScoped() =>
        new ScopedObservableDictionaryQuery<TKey, TValue>(this);

    public virtual object SyncRoot =>
        throw new NotSupportedException("The SyncRoot property may not be used for the synchronization of observable queries.");

    public abstract IEnumerable<TValue> Values { get; }

    ICollection<TValue> IDictionary<TKey, TValue>.Values =>
        Values.ToList().AsReadOnly();

    public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? DictionaryChanged
    {
        add
        {
            lock (changeAccess)
            {
                if (dictionaryChanged is null)
                    OnChangeObservationBegan(ObservableDictionaryChangeObservation.Dictionary);
                dictionaryChanged += value;
            }
        }
        remove
        {
            lock (changeAccess)
            {
                dictionaryChanged -= value;
                if (dictionaryChanged is null)
                    OnChangeObservationEnded(ObservableDictionaryChangeObservation.Dictionary);
            }
        }
    }

    public event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? DictionaryChangedBoxed
    {
        add
        {
            lock (changeAccess)
            {
                if (dictionaryChangedBoxed is null)
                    OnChangeObservationBegan(ObservableDictionaryChangeObservation.BoxedDictionary);
                dictionaryChangedBoxed += value;
            }
        }
        remove
        {
            lock (changeAccess)
            {
                dictionaryChangedBoxed -= value;
                if (dictionaryChangedBoxed is null)
                    OnChangeObservationEnded(ObservableDictionaryChangeObservation.BoxedDictionary);
            }
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            lock (changeAccess)
            {
                if (collectionChanged is null)
                    OnChangeObservationBegan(ObservableDictionaryChangeObservation.Collection);
                collectionChanged += value;
            }
        }
        remove
        {
            lock (changeAccess)
            {
                collectionChanged -= value;
                if (collectionChanged is null)
                    OnChangeObservationEnded(ObservableDictionaryChangeObservation.Collection);
            }
        }
    }

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => DictionaryChangedBoxed += value;
        remove => DictionaryChangedBoxed -= value;
    }

    public abstract bool Contains(KeyValuePair<TKey, TValue> item);

    public abstract bool ContainsKey(TKey key);

    public abstract void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex);

    public abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public abstract IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys);

    protected virtual void OnChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (collectionChanged is not null)
            switch (e.Action)
            {
                case NotifyDictionaryChangedAction.Add:
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)e.NewItems));
                    break;
                case NotifyDictionaryChangedAction.Remove:
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)e.OldItems));
                    break;
                case NotifyDictionaryChangedAction.Replace:
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, (IList)e.NewItems, (IList)e.OldItems));
                    break;
                case NotifyDictionaryChangedAction.Reset:
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    break;
            }
        if (dictionaryChangedBoxed is not null)
            switch (e.Action)
            {
                case NotifyDictionaryChangedAction.Add:
                    OnDictionaryChangedBoxed(new NotifyDictionaryChangedEventArgs<object?, object?>(NotifyDictionaryChangedAction.Add, e.NewItems.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value))));
                    break;
                case NotifyDictionaryChangedAction.Remove:
                    OnDictionaryChangedBoxed(new NotifyDictionaryChangedEventArgs<object?, object?>(NotifyDictionaryChangedAction.Remove, e.OldItems.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value))));
                    break;
                case NotifyDictionaryChangedAction.Replace:
                    OnDictionaryChangedBoxed(new NotifyDictionaryChangedEventArgs<object?, object?>(NotifyDictionaryChangedAction.Replace, e.NewItems.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value)), e.OldItems.Select(kv => new KeyValuePair<object?, object?>(kv.Key, kv.Value))));
                    break;
                case NotifyDictionaryChangedAction.Reset:
                    OnDictionaryChangedBoxed(new NotifyDictionaryChangedEventArgs<object?, object?>(NotifyDictionaryChangedAction.Reset));
                    break;
            }
        OnDictionaryChanged(e);
    }

    /// <summary>
    /// Called when one of the events with which this query describes a change gains its first subscriber, so that a query which produces that event's arguments at a cost may produce only the ones somebody is listening for
    /// </summary>
    private protected virtual void OnChangeObservationBegan(ObservableDictionaryChangeObservation observation)
    {
    }

    /// <summary>
    /// Called when one of the events with which this query describes a change loses its last subscriber
    /// </summary>
    private protected virtual void OnChangeObservationEnded(ObservableDictionaryChangeObservation observation)
    {
    }

    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!DeferNotification(e))
            RaiseCollectionChanged(e);
    }

    protected virtual void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        if (!DeferNotification(e))
            RaiseDictionaryChanged(e);
    }

    protected virtual void OnDictionaryChangedBoxed(NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        if (!DeferNotification(new DeferredDictionaryChangedBoxed(e)))
            RaiseDictionaryChangedBoxed(e);
    }

    void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        var eventArgs = Logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        collectionChanged?.Invoke(this, e);
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    void RaiseDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingDictionaryChanged, "Raising DictionaryChanged: {EventArgs}", e);
        dictionaryChanged?.Invoke(this, e);
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedDictionaryChanged, "Raised DictionaryChanged: {EventArgs}", e);
    }

    void RaiseDictionaryChangedBoxed(NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        dictionaryChangedBoxed?.Invoke(this, e);

    private protected override void RaiseNotification(object eventArguments)
    {
        if (eventArguments is DeferredDictionaryChangedBoxed deferredDictionaryChangedBoxed)
            RaiseDictionaryChangedBoxed(deferredDictionaryChangedBoxed.EventArguments);
        else if (eventArguments is NotifyDictionaryChangedEventArgs<TKey, TValue> dictionaryChangedEventArgs)
            RaiseDictionaryChanged(dictionaryChangedEventArgs);
        else if (eventArguments is NotifyCollectionChangedEventArgs collectionChangedEventArgs)
            RaiseCollectionChanged(collectionChangedEventArgs);
        else
            base.RaiseNotification(eventArguments);
    }

    public abstract bool TryGetValue(TKey key, out TValue value);

    #region Unsupported Operations

    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) =>
        throw new NotSupportedException();

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
        throw new NotSupportedException();

    void ICollection<KeyValuePair<TKey, TValue>>.Clear() =>
        throw new NotSupportedException();

    bool IDictionary<TKey, TValue>.Remove(TKey key) =>
        throw new NotSupportedException();

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) =>
        throw new NotSupportedException();

    #endregion Unsupported Operations

    #region Observation Methods

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(seedFactory);
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(resultSelector);
        ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult> aggregateQuery;
        lock (CachedQueriesAccess)
        {
            var key = (seedFactory, func, resultSelector);
            if (!(cachedAggregateQueries ??= []).TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = new ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
                cachedAggregateQueries.Add(key, aggregateQuery);
            }
            else
                aggregateQuery = (ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult>)cachedAggregateQuery;
            ++aggregateQuery.Observations;
        }
        aggregateQuery.Initialize();
        return aggregateQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TKey, TValue, bool>>)optimizer(key);
        ObservableDictionaryAllQuery<TKey, TValue> allQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedAllQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out allQuery!))
            {
                allQuery = new ObservableDictionaryAllQuery<TKey, TValue>(collectionObserver, this, key);
                cachedAllQueries.Add(key, allQuery);
            }
            ++allQuery.Observations;
        }
        allQuery.Initialize();
        return allQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny()
    {
        ObservableDictionaryAnyQuery<TKey, TValue> anyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedAnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(null, out anyQuery!))
            {
                anyQuery = new ObservableDictionaryAnyQuery<TKey, TValue>(collectionObserver, this);
                cachedAnyQueries.Add(null, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TKey, TValue, bool>>)optimizer(key);
        ObservableDictionaryAnyQuery<TKey, TValue> anyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedAnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(key, out anyQuery!))
            {
                anyQuery = new ObservableDictionaryAnyQuery<TKey, TValue>(collectionObserver, this, key);
                cachedAnyQueries.Add(key, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveAverage() =>
        ObserveAverage(ValueLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        try
        {
            var averageQuery = toCollectionQuery.ObserveAverage();
            averageQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
            return averageQuery;
        }
        catch
        {
            toCollectionQuery.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TResult> ObserveCast<TResult>() =>
        ObserveSelect(CastLambdas<TResult>.Key, CastLambdas<TResult>.Value);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently()
    {
        lock (CachedQueriesAccess)
        {
            cachedConcurrentQuery ??= new ObservableDictionaryConcurrentQuery<TKey, TValue>(collectionObserver, this);
            ++cachedConcurrentQuery.Observations;
        }
        cachedConcurrentQuery.Initialize();
        return cachedConcurrentQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount()
    {
        lock (CachedQueriesAccess)
        {
            cachedCountQuery ??= new ObservableDictionaryCountQuery<TKey, TValue>(collectionObserver, this);
            ++cachedCountQuery.Observations;
        }
        cachedCountQuery.Initialize();
        return cachedCountQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount(Expression<Func<TKey, TValue, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var where = ObserveWhere(predicate);
        try
        {
            var count = where.ObserveCount();
            count.Disposed += (_, _) => where.Dispose();
            return count;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst() =>
        ObserveFirst(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveFirst(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(IComparer<TKey> comparer) =>
        ObserveKeyed(comparer, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var first = where.ObserveFirst(comparer);
            first.Disposed += (_, _) => where.Dispose();
            return first;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault() =>
        ObserveFirstOrDefault(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveFirstOrDefault(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(IComparer<TKey> comparer) =>
        ObserveKeyed(comparer, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var firstOrDefault = where.ObserveFirstOrDefault(comparer);
            firstOrDefault.Disposed += (_, _) => where.Dispose();
            return firstOrDefault;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveKeyed(IComparer<TKey>? keyComparer, bool notFoundIsDefault)
    {
        ObservableDictionaryKeyedQuery<TKey, TValue> keyedQuery;
        lock (CachedQueriesAccess)
        {
            var key = (keyComparer, notFoundIsDefault);
            if (!(cachedKeyedQueries ??= []).TryGetValue(key, out keyedQuery!))
            {
                keyedQuery = new ObservableDictionaryKeyedQuery<TKey, TValue>(collectionObserver, this, keyComparer, notFoundIsDefault);
                cachedKeyedQueries.Add(key, keyedQuery);
            }
            ++keyedQuery.Observations;
        }
        keyedQuery.Initialize();
        return keyedQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast() =>
        ObserveLast(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveLast(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> comparer) =>
        ObserveKeyed(ReversedComparers.For(comparer), false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var last = where.ObserveLast(comparer);
            last.Disposed += (_, _) => where.Dispose();
            return last;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault() =>
        ObserveLastOrDefault(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveLastOrDefault(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> comparer) =>
        ObserveKeyed(ReversedComparers.For(comparer), true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var lastOrDefault = where.ObserveLastOrDefault(comparer);
            lastOrDefault.Disposed += (_, _) => where.Dispose();
            return lastOrDefault;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveMax() =>
        ObserveMax(ValueLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        try
        {
            var maxQuery = toCollectionQuery.ObserveMax();
            maxQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
            return maxQuery;
        }
        catch
        {
            toCollectionQuery.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveMin() =>
        ObserveMin(ValueLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        try
        {
            var minQuery = toCollectionQuery.ObserveMin();
            minQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
            return minQuery;
        }
        catch
        {
            toCollectionQuery.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>()
    {
        var where = ObserveWhere(CastLambdas<TResult>.IsResult);
        try
        {
            var cast = where.ObserveCast<TResult>();
            cast.Disposed += (_, _) => where.Dispose();
            return cast;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector)
        where TResultKey : notnull =>
        ObserveSelect(keySelector, valueSelector, EqualityComparer<TResultKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector, IEqualityComparer<TResultKey> equalityComparer)
        where TResultKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(equalityComparer);
        var keyValuePairSelector = PairProjections<TResultKey, TResultValue>.For(keySelector, valueSelector);
        ObservableQuery selectQuery;
        var key = (keyValuePairSelector, equalityComparer);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = ((Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>)optimizer(keyValuePairSelector), equalityComparer);
        lock (CachedQueriesAccess)
        {
            if (!(cachedSelectQueries ??= new(CachedSelectQueryEqualityComparer.Default)).TryGetValue(key, out selectQuery!))
            {
                selectQuery = new ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue>(collectionObserver, this, key.keyValuePairSelector, key.equalityComparer);
                cachedSelectQueries.Add(key, selectQuery);
            }
            ++selectQuery.Observations;
        }
        selectQuery.Initialize();
        return ((ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue>)selectQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle() =>
        ObserveKeyed(null, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var single = where.ObserveSingle();
            single.Disposed += (_, _) => where.Dispose();
            return single;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault() =>
        ObserveKeyed(null, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate)
    {
        var where = ObserveWhere(predicate);
        try
        {
            var singleOrDefault = where.ObserveSingleOrDefault();
            singleOrDefault.Disposed += (_, _) => where.Dispose();
            return singleOrDefault;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveSum() =>
        ObserveSum(ValueLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        try
        {
            var sumQuery = toCollectionQuery.ObserveSum();
            sumQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
            return sumQuery;
        }
        catch
        {
            toCollectionQuery.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TValue> ObserveToCollection() =>
        ObserveToCollection(ValueLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Expression<Func<TKey, TValue, TElement>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObservableQuery toCollectionQuery;
        var key = PairSelectors<TElement>.For(selector);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<KeyValuePair<TKey, TValue>, TElement>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedToCollectionQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out toCollectionQuery!))
            {
                toCollectionQuery = new ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>(collectionObserver, this, key);
                cachedToCollectionQueries.Add(key, toCollectionQuery);
            }
            ++toCollectionQuery.Observations;
        }
        toCollectionQuery.Initialize();
        return ((ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>)toCollectionQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue> usingSynchronizationContextQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationContextQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                usingSynchronizationContextQuery = new ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextQueries.Add(synchronizationContext, usingSynchronizationContextQuery);
            }
            ++usingSynchronizationContextQuery.Observations;
        }
        usingSynchronizationContextQuery.Initialize();
        return usingSynchronizationContextQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue> usingSynchronizationContextEventuallyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationContextEventuallyQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                usingSynchronizationContextEventuallyQuery = new ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextEventuallyQueries.Add(synchronizationContext, usingSynchronizationContextEventuallyQuery);
            }
            ++usingSynchronizationContextEventuallyQuery.Observations;
        }
        usingSynchronizationContextEventuallyQuery.Initialize();
        return usingSynchronizationContextEventuallyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveValueFor(TKey key) =>
        ObserveValueFor(key, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveValueForOrDefault(TKey key) =>
        ObserveValueFor(key, true);

    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveValueFor(TKey key, bool notFoundIsDefault)
    {
        ArgumentNullException.ThrowIfNull(key);
        ObservableDictionaryValueForQuery<TKey, TValue> valueForQuery;
        var cacheKey = (key, notFoundIsDefault);
        lock (CachedQueriesAccess)
        {
            if (!(cachedValueForQueries ??= []).TryGetValue(cacheKey, out valueForQuery!))
            {
                valueForQuery = new ObservableDictionaryValueForQuery<TKey, TValue>(collectionObserver, this, key, notFoundIsDefault);
                cachedValueForQueries.Add(cacheKey, valueForQuery);
            }
            ++valueForQuery.Observations;
        }
        valueForQuery.Initialize();
        return valueForQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObservableQuery whereQuery;
        var key = PairPredicates.For(predicate);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<KeyValuePair<TKey, TValue>, bool>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedWhereQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out whereQuery!))
            {
                whereQuery = new ObservableDictionaryWhereQuery<TKey, TValue>(collectionObserver, this, key);
                cachedWhereQueries.Add(key, whereQuery);
            }
            ++whereQuery.Observations;
        }
        whereQuery.Initialize();
        return ((ObservableDictionaryWhereQuery<TKey, TValue>)whereQuery).AsScoped();
    }

    #endregion Observation Methods

    #region Query Disposal Methods

    internal bool QueryDisposed<TAccumulate, TResult>(ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult> aggregateQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --aggregateQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAggregateQueries!.Remove((aggregateQuery.SeedFactory, aggregateQuery.Func, aggregateQuery.ResultSelector));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryAllQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAllQueries!.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryAnyQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAnyQueries!.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryConcurrentQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedConcurrentQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryCountQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedCountQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryKeyedQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedKeyedQueries!.Remove((query.KeyComparer, query.NotFoundIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResultKey, TResultValue>(ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue> query)
        where TResultKey : notnull
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedSelectQueries!.Remove((query.KeyValuePairSelector, query.EqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TElement>(ObservableDictionaryToCollectionQuery<TElement, TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedToCollectionQueries!.Remove(query.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue> usingSynchronizationContextEventuallyQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSynchronizationContextEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSynchronizationContextEventuallyQueries!.Remove(usingSynchronizationContextEventuallyQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue> usingSynchronizationContextQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSynchronizationContextQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSynchronizationContextQueries!.Remove(usingSynchronizationContextQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryValueForQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedValueForQueries!.Remove((query.Key, query.NotFoundIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryWhereQuery<TKey, TValue> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedWhereQueries!.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }

    #endregion Query Disposal Methods
}
