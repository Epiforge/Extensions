namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableDictionaryQuery<TKey, TValue> :
    ObservableQuery,
    IObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
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
#if IS_NET_STANDARD_2_1_OR_GREATER
            var hashCode = new System.HashCode();
#else
            var hashCode = new Components.HashCode();
#endif
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.keyValuePairSelector));
            hashCode.Add(obj.equalityComparer);
            return hashCode.ToHashCode();
        }
    }

    #endregion Cache Comparers

    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    protected ObservableDictionaryQuery(CollectionObserver collectionObserver) :
        base(collectionObserver)
    {
    }

    readonly Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery> cachedAggregateQueries = new();
    readonly object cachedAggregateQueriesAccess = new();
    readonly Dictionary<Expression, ObservableDictionaryAllQuery<TKey, TValue>> cachedAllQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedAllQueriesAccess = new();
    readonly NullableKeyDictionary<Expression?, ObservableDictionaryAnyQuery<TKey, TValue>> cachedAnyQueries = new(ExpressionEqualityComparer.Default!);
    readonly object cachedAnyQueriesAccess = new();
    ObservableDictionaryConcurrentQuery<TKey, TValue>? cachedConcurrentQuery;
    readonly object cachedConcurrentQueryAccess = new();
    ObservableDictionaryCountQuery<TKey, TValue>? cachedCountQuery;
    readonly object cachedCountQueryAccess = new();
    readonly Dictionary<(IComparer<TKey>? keyComparer, bool notFoundIsDefault), ObservableDictionaryKeyedQuery<TKey, TValue>> cachedKeyedQueries = new();
    readonly object cachedKeyedQueriesAccess = new();
    readonly Dictionary<(Expression keyValuePairSelector, object equalityComparer), ObservableQuery> cachedSelectQueries = new(CachedSelectQueryEqualityComparer.Default);
    readonly object cachedSelectQueriesAccess = new();
    readonly Dictionary<Expression, ObservableQuery> cachedToCollectionQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedToCollectionQueriesAccess = new();
    readonly Dictionary<SynchronizationContext, ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue>> cachedUsingSynchronizationContextEventuallyQueries = new();
    readonly object cachedUsingSynchronizationContextEventuallyQueriesAccess = new();
    readonly Dictionary<SynchronizationContext, ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue>> cachedUsingSynchronizationContextQueries = new();
    readonly object cachedUsingSynchronizationContextQueriesAccess = new();
    readonly Dictionary<(TKey key, bool notFoundIsDefault), ObservableDictionaryValueForQuery<TKey, TValue>> cachedValueForQueries = new();
    readonly object cachedValueForQueriesAccess = new();
    readonly Dictionary<Expression<Func<KeyValuePair<TKey, TValue>, bool>>, ObservableQuery> cachedWhereQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedWhereQueriesAccess = new();
    Exception? operationFault;

    public abstract TValue this[TKey key] { get; }

    public override int CachedObservableQueries
    {
        get
        {
            var count = 0;
            lock (cachedAggregateQueriesAccess)
                count += cachedAggregateQueries.Values.Sum(aggregateQuery => 1 + aggregateQuery.CachedObservableQueries);
            lock (cachedAllQueriesAccess)
                count += cachedAllQueries.Values.Sum(allQuery => 1 + allQuery.CachedObservableQueries);
            lock (cachedAnyQueriesAccess)
                count += cachedAnyQueries.Values.Sum(anyQuery => 1 + anyQuery.CachedObservableQueries);
            lock (cachedConcurrentQueryAccess)
                count += cachedConcurrentQuery is null ? 0 : 1 + cachedConcurrentQuery.CachedObservableQueries;
            lock (cachedCountQueryAccess)
                count += cachedCountQuery is null ? 0 : 1 + cachedCountQuery.CachedObservableQueries;
            lock (cachedKeyedQueriesAccess)
                count += cachedKeyedQueries.Values.Sum(keyedQuery => 1 + keyedQuery.CachedObservableQueries);
            lock (cachedSelectQueriesAccess)
                count += cachedSelectQueries.Values.Sum(selectQuery => 1 + selectQuery.CachedObservableQueries);
            lock (cachedToCollectionQueriesAccess)
                count += cachedToCollectionQueries.Values.Sum(toCollectionQuery => 1 + toCollectionQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationContextEventuallyQueriesAccess)
                count += cachedUsingSynchronizationContextEventuallyQueries.Values.Sum(usingSynchronizationContextEventuallyQuery => 1 + usingSynchronizationContextEventuallyQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationContextQueriesAccess)
                count += cachedUsingSynchronizationContextQueries.Values.Sum(usingSynchronizationContextQuery => 1 + usingSynchronizationContextQuery.CachedObservableQueries);
            lock (cachedValueForQueriesAccess)
                count += cachedValueForQueries.Values.Sum(valueQuery => 1 + valueQuery.CachedObservableQueries);
            lock (cachedWhereQueriesAccess)
                count += cachedWhereQueries.Values.Sum(whereQuery => 1 + whereQuery.CachedObservableQueries);
            return count;
        }
    }

    public abstract int Count { get; }

    public virtual bool IsSynchronized =>
        false;

    public abstract IEnumerable<TKey> Keys { get; }

    public virtual Exception? OperationFault
    {
        get => operationFault;
        protected set => SetBackedProperty(ref operationFault, in value, operationFaultPropertyChangingEventArgs, operationFaultPropertyChangedEventArgs);
    }

    public virtual object SyncRoot =>
        null!;

    public abstract IEnumerable<TValue> Values { get; }

    public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? DictionaryChanged;

    public abstract bool ContainsKey(TKey key);

    public abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    protected void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingDictionaryChanged, "Raising DictionaryChanged: {EventArgs}", e);
        DictionaryChanged?.Invoke(this, e);
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedDictionaryChanged, "Raised DictionaryChanged: {EventArgs}", e);
    }

    public abstract bool TryGetValue(TKey key, out TValue value);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(seedFactory);
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(resultSelector);
#else
        if (seedFactory is null)
            throw new ArgumentNullException(nameof(seedFactory));
        if (func is null)
            throw new ArgumentNullException(nameof(func));
        if (resultSelector is null)
            throw new ArgumentNullException(nameof(resultSelector));
#endif
        ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult> aggregateQuery;
        lock (cachedAggregateQueriesAccess)
        {
            var key = (seedFactory, func, resultSelector);
            if (!cachedAggregateQueries.TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = new ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
                cachedAggregateQueries.Add(key, aggregateQuery);
            }
            else
                aggregateQuery = (ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult>)cachedAggregateQuery;
            ++aggregateQuery.Observations;
        }
        aggregateQuery.Initialize();
        return aggregateQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TKey, TValue, bool>>)optimizer(key);
        ObservableDictionaryAllQuery<TKey, TValue> allQuery;
        lock (cachedAllQueriesAccess)
        {
            if (!cachedAllQueries.TryGetValue(predicate, out allQuery!))
            {
                allQuery = new ObservableDictionaryAllQuery<TKey, TValue>(collectionObserver, this, key);
                cachedAllQueries.Add(predicate, allQuery);
            }
            ++allQuery.Observations;
        }
        allQuery.Initialize();
        return allQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny()
    {
        ObservableDictionaryAnyQuery<TKey, TValue> anyQuery;
        lock (cachedAnyQueriesAccess)
        {
            if (!cachedAnyQueries.TryGetValue(null, out anyQuery!))
            {
                anyQuery = new ObservableDictionaryAnyQuery<TKey, TValue>(collectionObserver, this);
                cachedAnyQueries.Add(null, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TKey, TValue, bool>>)optimizer(key);
        ObservableDictionaryAnyQuery<TKey, TValue> anyQuery;
        lock (cachedAnyQueriesAccess)
        {
            if (!cachedAnyQueries.TryGetValue(predicate, out anyQuery!))
            {
                anyQuery = new ObservableDictionaryAnyQuery<TKey, TValue>(collectionObserver, this, key);
                cachedAnyQueries.Add(predicate, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveAverage() =>
        ObserveAverage((key, value) => value);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        var averageQuery = toCollectionQuery.ObserveAverage();
        averageQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
        return averageQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TResult> ObserveCast<TResult>() =>
        ObserveSelect((key, value) => key, (key, value) => (TResult)(object)value!);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently()
    {
        lock (cachedConcurrentQueryAccess)
        {
            cachedConcurrentQuery ??= new ObservableDictionaryConcurrentQuery<TKey, TValue>(collectionObserver, this);
            ++cachedConcurrentQuery.Observations;
        }
        cachedConcurrentQuery.Initialize();
        return cachedConcurrentQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount()
    {
        lock (cachedCountQueryAccess)
        {
            cachedCountQuery ??= new ObservableDictionaryCountQuery<TKey, TValue>(collectionObserver, this);
            ++cachedCountQuery.Observations;
        }
        cachedCountQuery.Initialize();
        return cachedCountQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount(Expression<Func<TKey, TValue, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var count = where.ObserveCount();
        count.Disposed += (_, _) => where.Dispose();
        return count;
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
        var first = where.ObserveFirst(comparer);
        first.Disposed += (_, _) => where.Dispose();
        return first;
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
        var firstOrDefault = where.ObserveFirstOrDefault(comparer);
        firstOrDefault.Disposed += (_, _) => where.Dispose();
        return firstOrDefault;
    }

    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveKeyed(IComparer<TKey>? keyComparer, bool notFoundIsDefault)
    {
        ObservableDictionaryKeyedQuery<TKey, TValue> keyedQuery;
        lock (cachedKeyedQueriesAccess)
        {
            var key = (keyComparer, notFoundIsDefault);
            if (!cachedKeyedQueries.TryGetValue(key, out keyedQuery!))
            {
                keyedQuery = new ObservableDictionaryKeyedQuery<TKey, TValue>(collectionObserver, this, keyComparer, notFoundIsDefault);
                cachedKeyedQueries.Add(key, keyedQuery);
            }
            ++keyedQuery.Observations;
        }
        keyedQuery.Initialize();
        return keyedQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast() =>
        ObserveLast(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveLast(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> comparer) =>
        ObserveKeyed(new ReversedComparer<TKey>(comparer), false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        var last = where.ObserveLast(comparer);
        last.Disposed += (_, _) => where.Dispose();
        return last;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault() =>
        ObserveLastOrDefault(Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        ObserveLastOrDefault(predicate, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> comparer) =>
        ObserveKeyed(new ReversedComparer<TKey>(comparer), true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer)
    {
        var where = ObserveWhere(predicate);
        var lastOrDefault = where.ObserveLastOrDefault(comparer);
        lastOrDefault.Disposed += (_, _) => where.Dispose();
        return lastOrDefault;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveMax() =>
        ObserveMax((key, value) => value);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        var maxQuery = toCollectionQuery.ObserveMax();
        maxQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
        return maxQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveMin() =>
        ObserveMin((key, value) => value);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        var minQuery = toCollectionQuery.ObserveMin();
        minQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
        return minQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>()
    {
        var where = ObserveWhere((key, value) => value is TResult);
        var cast = where.ObserveCast<TResult>();
        cast.Disposed += (_, _) => where.Dispose();
        return cast;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector)
        where TResultKey : notnull =>
        ObserveSelect(keySelector, valueSelector, EqualityComparer<TResultKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector, IEqualityComparer<TResultKey> equalityComparer)
        where TResultKey : notnull
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(equalityComparer);
#else
        if (keySelector is null)
            throw new ArgumentNullException(nameof(keySelector));
        if (valueSelector is null)
            throw new ArgumentNullException(nameof(valueSelector));
        if (equalityComparer is null)
            throw new ArgumentNullException(nameof(equalityComparer));
#endif
        var sourceKeyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
        var sourceKeyExpression = Expression.Property(sourceKeyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Key));
        var sourceValueExpression = Expression.Property(sourceKeyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Value));
        var keyExpression = Expression.Invoke(keySelector, sourceKeyExpression, sourceValueExpression);
        var valueExpression = Expression.Invoke(valueSelector, sourceKeyExpression, sourceValueExpression);
        var keyValuePairExpression = Expression.New(typeof(KeyValuePair<TResultKey, TResultValue>).GetConstructor(new[] { typeof(TResultKey), typeof(TResultValue) })!, keyExpression, valueExpression);
        var keyValuePairSelector = Expression.Lambda<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>(keyValuePairExpression, sourceKeyValuePairParameter);

        ObservableQuery selectQuery;
        var key = (keyValuePairSelector, equalityComparer);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = ((Expression<Func<KeyValuePair<TKey, TValue>, KeyValuePair<TResultKey, TResultValue>>>)optimizer(keyValuePairSelector), equalityComparer);
        lock (cachedSelectQueriesAccess)
        {
            if (!cachedSelectQueries.TryGetValue(key, out selectQuery!))
            {
                selectQuery = new ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue>(collectionObserver, this, key.keyValuePairSelector, key.equalityComparer);
                cachedSelectQueries.Add(key, selectQuery);
            }
            ++selectQuery.Observations;
        }
        selectQuery.Initialize();
        return (ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue>)selectQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle() =>
        ObserveKeyed(null, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate)
    {
        var where = ObserveWhere(predicate);
        var single = where.ObserveSingle();
        single.Disposed += (_, _) => where.Dispose();
        return single;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault() =>
        ObserveKeyed(null, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate)
    {
        var where = ObserveWhere(predicate);
        var singleOrDefault = where.ObserveSingleOrDefault();
        singleOrDefault.Disposed += (_, _) => where.Dispose();
        return singleOrDefault;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TValue> ObserveSum() =>
        ObserveSum((key, value) => value);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector)
    {
        var toCollectionQuery = ObserveToCollection(selector);
        var sumQuery = toCollectionQuery.ObserveSum();
        sumQuery.Disposed += (_, _) => toCollectionQuery.Dispose();
        return sumQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TValue> ObserveToCollection() =>
        ObserveToCollection((key, value) => value);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Expression<Func<TKey, TValue, TElement>> selector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        var keyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
        var keyExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Key));
        var valueExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Value));
        var invokeSelectorExpression = Expression.Invoke(selector, keyExpression, valueExpression);
        ObservableQuery toCollectionQuery;
        var key = Expression.Lambda<Func<KeyValuePair<TKey, TValue>, TElement>>(invokeSelectorExpression, keyValuePairParameter);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<KeyValuePair<TKey, TValue>, TElement>>)optimizer(key);
        lock (cachedToCollectionQueriesAccess)
        {
            if (!cachedToCollectionQueries.TryGetValue(key, out toCollectionQuery!))
            {
                toCollectionQuery = new ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>(collectionObserver, this, key);
                cachedToCollectionQueries.Add(key, toCollectionQuery);
            }
            ++toCollectionQuery.Observations;
        }
        toCollectionQuery.Initialize();
        return (ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>)toCollectionQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(synchronizationContext);
#else
        if (synchronizationContext is null)
            throw new ArgumentNullException(nameof(synchronizationContext));
#endif
        ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue> usingSynchronizationContextEventuallyQuery;
        lock (cachedUsingSynchronizationContextEventuallyQueriesAccess)
        {
            if (!cachedUsingSynchronizationContextEventuallyQueries.TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                usingSynchronizationContextEventuallyQuery = new ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextEventuallyQueries.Add(synchronizationContext, usingSynchronizationContextEventuallyQuery);
            }
            ++usingSynchronizationContextEventuallyQuery.Observations;
        }
        usingSynchronizationContextEventuallyQuery.Initialize();
        return usingSynchronizationContextEventuallyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(synchronizationContext);
#else
        if (synchronizationContext is null)
            throw new ArgumentNullException(nameof(synchronizationContext));
#endif
        ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue> usingSynchronizationContextQuery;
        lock (cachedUsingSynchronizationContextQueriesAccess)
        {
            if (!cachedUsingSynchronizationContextQueries.TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                usingSynchronizationContextQuery = new ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextQueries.Add(synchronizationContext, usingSynchronizationContextQuery);
            }
            ++usingSynchronizationContextQuery.Observations;
        }
        usingSynchronizationContextQuery.Initialize();
        return usingSynchronizationContextQuery;
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(key);
#else
        if (key is null)
            throw new ArgumentNullException(nameof(key));
#endif
        ObservableDictionaryValueForQuery<TKey, TValue> valueForQuery;
        var cacheKey = (key, notFoundIsDefault);
        lock (cachedValueForQueriesAccess)
        {
            if (!cachedValueForQueries.TryGetValue(cacheKey, out valueForQuery!))
            {
                valueForQuery = new ObservableDictionaryValueForQuery<TKey, TValue>(collectionObserver, this, key, notFoundIsDefault);
                cachedValueForQueries.Add(cacheKey, valueForQuery);
            }
            ++valueForQuery.Observations;
        }
        valueForQuery.Initialize();
        return valueForQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var keyValuePairParameter = Expression.Parameter(typeof(KeyValuePair<TKey, TValue>));
        var keyExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Key));
        var valueExpression = Expression.Property(keyValuePairParameter, nameof(KeyValuePair<TKey, TValue>.Value));
        var invokePredicateExpression = Expression.Invoke(predicate, keyExpression, valueExpression);
        ObservableQuery whereQuery;
        var key = Expression.Lambda<Func<KeyValuePair<TKey, TValue>, bool>>(invokePredicateExpression, keyValuePairParameter);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<KeyValuePair<TKey, TValue>, bool>>)optimizer(key);
        lock (cachedWhereQueriesAccess)
        {
            if (!cachedWhereQueries.TryGetValue(key, out whereQuery!))
            {
                whereQuery = new ObservableDictionaryWhereQuery<TKey, TValue>(collectionObserver, this, key);
                cachedWhereQueries.Add(key, whereQuery);
            }
            ++whereQuery.Observations;
        }
        whereQuery.Initialize();
        return (ObservableDictionaryWhereQuery<TKey, TValue>)whereQuery;
    }

    internal bool QueryDisposed<TAccumulate, TResult>(ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult> aggregateQuery)
    {
        lock (cachedAggregateQueriesAccess)
        {
            if (--aggregateQuery.Observations == 0)
            {
                cachedAggregateQueries.Remove((aggregateQuery.SeedFactory, aggregateQuery.Func, aggregateQuery.ResultSelector));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryAllQuery<TKey, TValue> query)
    {
        lock (cachedAllQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedAllQueries.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryAnyQuery<TKey, TValue> query)
    {
        lock (cachedAnyQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedAnyQueries.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryConcurrentQuery<TKey, TValue> query)
    {
        lock (cachedConcurrentQueryAccess)
        {
            if (--query.Observations == 0)
            {
                cachedConcurrentQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryKeyedQuery<TKey, TValue> query)
    {
        lock (cachedKeyedQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedKeyedQueries.Remove((query.KeyComparer, query.NotFoundIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResultKey, TResultValue>(ObservableDictionarySelectQuery<TResultKey, TResultValue, TKey, TValue> query)
        where TResultKey : notnull
    {
        lock (cachedSelectQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedSelectQueries.Remove((query.KeyValuePairSelector, query.EqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TElement>(ObservableDictionaryToCollectionQuery<TElement, TKey, TValue> query)
    {
        lock (cachedToCollectionQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedToCollectionQueries.Remove(query.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue> usingSynchronizationContextEventuallyQuery)
    {
        lock (cachedUsingSynchronizationContextEventuallyQueriesAccess)
        {
            if (--usingSynchronizationContextEventuallyQuery.Observations == 0)
            {
                cachedUsingSynchronizationContextEventuallyQueries.Remove(usingSynchronizationContextEventuallyQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue> usingSynchronizationContextQuery)
    {
        lock (cachedUsingSynchronizationContextQueriesAccess)
        {
            if (--usingSynchronizationContextQuery.Observations == 0)
            {
                cachedUsingSynchronizationContextQueries.Remove(usingSynchronizationContextQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryValueForQuery<TKey, TValue> query)
    {
        lock (cachedValueForQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedValueForQueries.Remove((query.Key, query.NotFoundIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableDictionaryWhereQuery<TKey, TValue> query)
    {
        lock (cachedWhereQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedWhereQueries.Remove(query.Predicate);
                return true;
            }
        }
        return false;
    }
}
