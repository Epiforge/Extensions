namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableCollectionQuery<TElement> :
    ObservableQuery,
    IObservableCollectionQuery<TElement>
{
    #region Cache Comparers

    sealed class CachedGroupByQueryEqualityComparer :
        IEqualityComparer<(Expression keySelector, object keyEqualityComaprer)>
    {
        public static CachedGroupByQueryEqualityComparer Default { get; } = new();

        public bool Equals((Expression keySelector, object keyEqualityComaprer) x, (Expression keySelector, object keyEqualityComaprer) y) =>
            ExpressionEqualityComparer.Default.Equals(x.keySelector, y.keySelector) && ReferenceEquals(x.keyEqualityComaprer, y.keyEqualityComaprer);

        public int GetHashCode([DisallowNull] (Expression keySelector, object keyEqualityComaprer) obj)
        {
#if IS_NET_STANDARD_2_1_OR_GREATER
            var hashCode = new System.HashCode();
#else
        var hashCode = new Components.HashCode();
#endif
            hashCode.Add(ExpressionEqualityComparer.Default.GetHashCode(obj.keySelector));
            hashCode.Add(obj.keyEqualityComaprer?.GetHashCode() ?? 0);
            return hashCode.ToHashCode();
        }
    }

    class CachedOrderByQueryEqualityComparer :
        IEqualityComparer<IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>>
    {
        public static CachedOrderByQueryEqualityComparer Default { get; } = new();

        public bool Equals(IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>? x, IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>? y)
        {
            if (x is null)
                return y is null;
            if (y is null)
                return false;
            if (x.Count != y.Count)
                return false;
            for (var i = 0; i < x.Count; ++i)
            {
                var (xSelector, xIsDescending) = x[i];
                var (ySelector, yIsDescending) = y[i];
                if (xIsDescending != yIsDescending)
                    return false;
                if (!ExpressionEqualityComparer.Default.Equals(xSelector, ySelector))
                    return false;
            }
            return true;
        }

        public int GetHashCode([DisallowNull] IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)> obj)
        {
#if IS_NET_STANDARD_2_1_OR_GREATER
            var hashCode = new System.HashCode();
#else
            var hashCode = new Components.HashCode();
#endif
            foreach (var (selector, isDescending) in obj)
            {
                hashCode.Add(ExpressionEqualityComparer.Default.GetHashCode(selector));
                hashCode.Add(isDescending);
            }
            return hashCode.ToHashCode();
        }
    }

    class CachedToDictionaryQueryEqualityComparer :
        IEqualityComparer<(Expression keySelector, Expression valueSelector, object equalityComparer)>
    {
        public static CachedToDictionaryQueryEqualityComparer Default { get; } = new();

        public bool Equals((Expression keySelector, Expression valueSelector, object equalityComparer) x, (Expression keySelector, Expression valueSelector, object equalityComparer) y)
        {
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            return expressionEqualityComparer.Equals(x.keySelector, y.keySelector) && expressionEqualityComparer.Equals(x.valueSelector, y.valueSelector) && ReferenceEquals(x.equalityComparer, y.equalityComparer);
        }

        public int GetHashCode([DisallowNull] (Expression keySelector, Expression valueSelector, object equalityComparer) obj)
        {
#if IS_NET_STANDARD_2_1_OR_GREATER
            var hashCode = new System.HashCode();
#else
            var hashCode = new Components.HashCode();
#endif
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.keySelector));
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.valueSelector));
            hashCode.Add(obj.equalityComparer);
            return hashCode.ToHashCode();
        }
    }

    #endregion Cache Comparers

    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    public ObservableCollectionQuery(CollectionObserver collectionObserver) :
        base(collectionObserver)
    {
    }

    readonly Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery> cachedAggregateQueries = new();
    readonly object cachedAggregateQueriesAccess = new();
    readonly Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionAllQuery<TElement>> cachedAllQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedAllQueriesAccess = new();
    readonly NullableKeyDictionary<Expression<Func<TElement, bool>>?, ObservableCollectionAnyQuery<TElement>> cachedAnyQueries = new(ExpressionEqualityComparer.Default!);
    readonly object cachedAnyQueriesAccess = new();
    readonly Dictionary<Expression, ObservableQuery> cachedAverageQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedAverageQueriesAccess = new();
    readonly Dictionary<int, ObservableCollectionComparisonQuery<TElement>> cachedComparisonQueries = new();
    readonly object cachedComparisonQueriesAccess = new();
    readonly Dictionary<IObservableCollectionQuery<TElement>, ObservableCollectionConcatQuery<TElement>> cachedConcatQueries = new();
    readonly object cachedConcatQueriesAccess = new();
    readonly Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery> cachedGroupByQueries = new(CachedGroupByQueryEqualityComparer.Default);
    readonly object cachedGroupByQueriesAccess = new();
    readonly Dictionary<(Index? index, bool outOfRangeIsDefault), ObservableQuery> cachedIndexQueries = new();
    readonly object cachedIndexQueriesAccess = new();
    readonly Dictionary<IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>, ObservableCollectionOrderByQuery<TElement>> cachedOrderByQueries = new(CachedOrderByQueryEqualityComparer.Default);
    ObservableCollectionIndividualChangesQuery<TElement>? cachedIndividualChangeQuery;
    readonly object cachedIndividualChangeQueryAccess = new();
    readonly object cachedOrderByQueriesAccess = new();
    readonly Dictionary<Expression, ObservableQuery> cachedSelectQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedSelectQueriesAccess = new();
    readonly Dictionary<Expression, ObservableQuery> cachedSelectManyQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedSelectManyQueriesAccess = new();
    readonly Dictionary<Expression, ObservableQuery> cachedSumQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedSumQueriesAccess = new();
    readonly Dictionary<(Expression keySelector, Expression valueSelector, object equalityComparer), ObservableQuery> cachedToDictionaryQueries = new(CachedToDictionaryQueryEqualityComparer.Default);
    readonly object cachedToDictionaryQueriesAccess = new();
    readonly Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>> cachedUsingSynchronizationCallbackEventuallyQueries = new();
    readonly object cachedUsingSynchronizationCallbackEventuallyQueriesAccess = new();
    readonly Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackQuery<TElement>> cachedUsingSynchronizationCallbackQueries = new();
    readonly object cachedUsingSynchronizationCallbackQueriesAccess = new();
    readonly Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>> cachedUsingSynchronizationContextEventuallyQueries = new();
    readonly object cachedUsingSynchronizationContextEventuallyQueriesAccess = new();
    readonly Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextQuery<TElement>> cachedUsingSynchronizationContextQueries = new();
    readonly object cachedUsingSynchronizationContextQueriesAccess = new();
    readonly Dictionary<object, ObservableCollectionUsingSyncRootEventuallyQuery<TElement>> cachedUsingSyncRootEventuallyQueries = new();
    readonly object cachedUsingSyncRootEventuallyQueriesAccess = new();
    readonly Dictionary<object, ObservableCollectionUsingSyncRootQuery<TElement>> cachedUsingSyncRootQueries = new();
    readonly object cachedUsingSyncRootQueriesAccess = new();
    readonly Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionWhereQuery<TElement>> cachedWhereQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedWhereQueriesAccess = new();
    Exception? operationFault;

    public abstract TElement this[int index] { get; }

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
            lock (cachedAverageQueriesAccess)
                count += cachedAverageQueries.Values.Sum(averageQuery => 1 + averageQuery.CachedObservableQueries);
            lock (cachedComparisonQueriesAccess)
                count += cachedComparisonQueries.Values.Sum(comparerQuery => 1 + comparerQuery.CachedObservableQueries);
            lock (cachedConcatQueriesAccess)
                count += cachedConcatQueries.Values.Sum(concatQuery => 1 + concatQuery.CachedObservableQueries);
            lock (cachedGroupByQueriesAccess)
                count += cachedGroupByQueries.Values.Sum(groupByQuery => 1 + groupByQuery.CachedObservableQueries);
            lock (cachedIndexQueriesAccess)
                count += cachedIndexQueries.Values.Sum(indexQuery => 1 + indexQuery.CachedObservableQueries);
            lock (cachedIndividualChangeQueryAccess)
                count += cachedIndividualChangeQuery is { } nonNullIndividualChangeQuery ? 1 + nonNullIndividualChangeQuery.CachedObservableQueries : 0;
            lock (cachedOrderByQueriesAccess)
                count += cachedOrderByQueries.Values.Sum(orderByQuery => 1 + orderByQuery.CachedObservableQueries);
            lock (cachedSelectQueriesAccess)
                count += cachedSelectQueries.Values.Sum(selectQuery => 1 + selectQuery.CachedObservableQueries);
            lock (cachedSelectManyQueriesAccess)
                count += cachedSelectManyQueries.Values.Sum(selectManyQuery => 1 + selectManyQuery.CachedObservableQueries);
            lock (cachedSumQueriesAccess)
                count += cachedSumQueries.Values.Sum(sumQuery => 1 + sumQuery.CachedObservableQueries);
            lock (cachedToDictionaryQueriesAccess)
                count += cachedToDictionaryQueries.Values.Sum(toDictionaryQuery => 1 + toDictionaryQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationCallbackEventuallyQueriesAccess)
                count += cachedUsingSynchronizationCallbackEventuallyQueries.Values.Sum(usingSynchronizationCallbackEventuallyQuery => 1 + usingSynchronizationCallbackEventuallyQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationCallbackQueriesAccess)
                count += cachedUsingSynchronizationCallbackQueries.Values.Sum(usingSynchronizationCallbackQuery => 1 + usingSynchronizationCallbackQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationContextEventuallyQueriesAccess)
                count += cachedUsingSynchronizationContextEventuallyQueries.Values.Sum(usingSynchronizationContextEventuallyQuery => 1 + usingSynchronizationContextEventuallyQuery.CachedObservableQueries);
            lock (cachedUsingSynchronizationContextQueriesAccess)
                count += cachedUsingSynchronizationContextQueries.Values.Sum(usingSynchronizationContextQuery => 1 + usingSynchronizationContextQuery.CachedObservableQueries);
            lock (cachedUsingSyncRootEventuallyQueriesAccess)
                count += cachedUsingSyncRootEventuallyQueries.Values.Sum(usingSyncRootEventuallyQuery => 1 + usingSyncRootEventuallyQuery.CachedObservableQueries);
            lock (cachedUsingSyncRootQueriesAccess)
                count += cachedUsingSyncRootQueries.Values.Sum(usingSyncRootQuery => 1 + usingSyncRootQuery.CachedObservableQueries);
            lock (cachedWhereQueriesAccess)
                count += cachedWhereQueries.Values.Sum(whereQuery => 1 + whereQuery.CachedObservableQueries);
            return count;
        }
    }

    public abstract int Count { get; }

    int ICollection.Count =>
        Count;

    int IReadOnlyCollection<TElement>.Count =>
        Count;

    internal virtual bool HasEnumerationPenalty =>
        false;

    internal virtual bool HasIndexerPenalty =>
        false;

    public virtual bool IsSynchronized =>
        false;

    public virtual Exception? OperationFault
    {
        get => operationFault;
        protected set => SetBackedProperty(ref operationFault, in value, operationFaultPropertyChangingEventArgs, operationFaultPropertyChangedEventArgs);
    }

    public virtual object SyncRoot =>
        null!;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public virtual void CopyTo(Array array, int index)
    {
        if (!HasIndexerPenalty)
            for (int i = 0, ii = Count; i < ii; ++i)
                array.SetValue(this[i], index + i);
        else
        {
            var i = index - 1;
            foreach (var item in this)
                array.SetValue(item, ++i);
        }
    }

    public abstract IEnumerator<TElement> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        var eventArgs = Logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        CollectionChanged?.Invoke(this, e);
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
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
        ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult> aggregateQuery;
        lock (cachedAggregateQueriesAccess)
        {
            var key = (seedFactory, func, resultSelector);
            if (!cachedAggregateQueries.TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = new ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
                cachedAggregateQueries.Add(key, aggregateQuery);
            }
            else
                aggregateQuery = (ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>)cachedAggregateQuery;
            ++aggregateQuery.Observations;
        }
        aggregateQuery.Initialize();
        return aggregateQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        ObservableCollectionAllQuery<TElement> allQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (cachedAllQueriesAccess)
        {
            if (!cachedAllQueries.TryGetValue(key, out allQuery!))
            {
                allQuery = new ObservableCollectionAllQuery<TElement>(collectionObserver, this, key);
                cachedAllQueries.Add(key, allQuery);
            }
            ++allQuery.Observations;
        }
        allQuery.Initialize();
        return allQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny()
    {
        ObservableCollectionAnyQuery<TElement> anyQuery;
        lock (cachedAnyQueriesAccess)
        {
            if (!cachedAnyQueries.TryGetValue(null, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this);
                cachedAnyQueries.Add(null, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        ObservableCollectionAnyQuery<TElement> anyQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (cachedAnyQueriesAccess)
        {
            if (!cachedAnyQueries.TryGetValue(key, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this, key);
                cachedAnyQueries.Add(key, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveAverage() =>
        ObserveAverage(element => element);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TElement, TResult>> selector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        ObservableQuery averageQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (cachedAverageQueriesAccess)
        {
            if (!cachedAverageQueries.TryGetValue(key, out averageQuery!))
            {
                averageQuery = new ObservableCollectionAverageQuery<TElement, TResult>(collectionObserver, this, key);
                cachedAverageQueries.Add(key, averageQuery);
            }
            ++averageQuery.Observations;
        }
        averageQuery.Initialize();
        return (ObservableCollectionAverageQuery<TElement, TResult>)averageQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveCast<TResult>() =>
        ObserveSelect(element => (TResult)(object)element!);

    [return: DisposeWhenDiscarded]
    internal IObservableScalarQuery<TElement> ObserveComparison(int soughtComparison)
    {
        ObservableCollectionComparisonQuery<TElement> comparisonQuery;
        lock (cachedComparisonQueriesAccess)
        {
            if (!cachedComparisonQueries.TryGetValue(soughtComparison, out comparisonQuery!))
            {
                comparisonQuery = new ObservableCollectionComparisonQuery<TElement>(collectionObserver, this, soughtComparison);
                cachedComparisonQueries.Add(soughtComparison, comparisonQuery);
            }
            ++comparisonQuery.Observations;
        }
        comparisonQuery.Initialize();
        return comparisonQuery;
    }

    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveComparison<TResult>(Expression<Func<TElement, TResult>> selector, int soughtComparison)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        var select = ObserveSelect(selector);
        var comparison = ((ObservableCollectionQuery<TResult>)select).ObserveComparison(soughtComparison);
        comparison.Disposed += (_, _) => select.Dispose();
        return comparison;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveConcat(IObservableCollectionQuery<TElement> second)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(second);
#else
        if (second is null)
            throw new ArgumentNullException(nameof(second));
#endif
        ObservableCollectionConcatQuery<TElement> concatQuery;
        lock (cachedConcatQueriesAccess)
        {
            if (!cachedConcatQueries.TryGetValue(second, out concatQuery!))
            {
                concatQuery = new ObservableCollectionConcatQuery<TElement>(collectionObserver, this, second);
                cachedConcatQueries.Add(second, concatQuery);
            }
            ++concatQuery.Observations;
        }
        concatQuery.Initialize();
        return concatQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveDistinct() =>
        ObserveDistinct(EqualityComparer<TElement>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveDistinct(IEqualityComparer<TElement> comparer)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(comparer);
#else
        if (comparer is null)
            throw new ArgumentNullException(nameof(comparer));
#endif
        var groupBy = ObserveGroupBy(element => element, comparer);
        var select = groupBy.ObserveSelect(group => group.Key);
        select.Disposed += (_, _) => groupBy.Dispose();
        return select;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveElementAt(int index) =>
        ObserveIndex(new Index(Math.Abs(index), index < 0), false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveElementAtOrDefault(int index) =>
        ObserveIndex(new Index(Math.Abs(index), index < 0), true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirst() =>
        ObserveIndex(0, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirst(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var first = where.ObserveFirst();
        first.Disposed += (_, _) => where.Dispose();
        return first;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirstOrDefault() =>
        ObserveIndex(0, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirstOrDefault(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var firstOrDefault = where.ObserveFirstOrDefault();
        firstOrDefault.Disposed += (_, _) => where.Dispose();
        return firstOrDefault;
    }

    IObservableScalarQuery<TElement> ObserveIndex(Index? index, bool outOfRangeIsDefault)
    {
        ObservableCollectionIndexQuery<TElement> indexQuery;
        lock (cachedIndexQueriesAccess)
        {
            if (!cachedIndexQueries.TryGetValue((index, outOfRangeIsDefault), out var cachedIndexQuery))
            {
                indexQuery = new ObservableCollectionIndexQuery<TElement>(collectionObserver, this, index, outOfRangeIsDefault);
                cachedIndexQueries.Add((index, outOfRangeIsDefault), indexQuery);
            }
            else
                indexQuery = (ObservableCollectionIndexQuery<TElement>)cachedIndexQuery;
            ++indexQuery.Observations;
        }
        indexQuery.Initialize();
        return indexQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveIndividualChanges()
    {
        lock (cachedIndividualChangeQueryAccess)
        {
            cachedIndividualChangeQuery ??= new ObservableCollectionIndividualChangesQuery<TElement>(collectionObserver, this);
            ++cachedIndividualChangeQuery.Observations;
        }
        cachedIndividualChangeQuery.Initialize();
        return cachedIndividualChangeQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector) =>
        ObserveGroupBy(keySelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(keyEqualityComparer);
#else
        if (keySelector is null)
            throw new ArgumentNullException(nameof(keySelector));
        if (keyEqualityComparer is null)
            throw new ArgumentNullException(nameof(keyEqualityComparer));
#endif
        ObservableQuery groupByQuery;
        var optimizedKeySelector = keySelector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            optimizedKeySelector = (Expression<Func<TElement, TKey>>)optimizer(optimizedKeySelector);
        var key = (optimizedKeySelector, keyEqualityComparer);
        lock (cachedGroupByQueriesAccess)
        {
            if (!cachedGroupByQueries.TryGetValue(key, out groupByQuery!))
            {
                groupByQuery = new ObservableCollectionGroupByQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
                cachedGroupByQueries.Add(key, groupByQuery);
            }
            ++groupByQuery.Observations;
        }
        groupByQuery.Initialize();
        return (ObservableCollectionGroupByQuery<TKey, TElement>)groupByQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLast() =>
        ObserveIndex(^1, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLast(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var last = where.ObserveLast();
        last.Disposed += (_, _) => where.Dispose();
        return last;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLastOrDefault() =>
        ObserveIndex(^1, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLastOrDefault(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var lastOrDefault = where.ObserveLastOrDefault();
        lastOrDefault.Disposed += (_, _) => where.Dispose();
        return lastOrDefault;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMax() =>
        ObserveComparison(1);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TElement, TResult>> selector) =>
        ObserveComparison(selector, 1);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMin() =>
        ObserveComparison(-1);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TElement, TResult>> selector) =>
        ObserveComparison(selector, -1);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveOfType<TResult>()
    {
        var where = ObserveWhere(element => element is TResult);
        var cast = where.ObserveCast<TResult>();
        cast.Disposed += (_, _) => where.Dispose();
        return cast;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector) =>
        ObserveOrderBy(selector, false);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector, bool isDescending) =>
        ObserveOrderBy((selector, isDescending));

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveOrderBy(params (Expression<Func<TElement, IComparable>> selector, bool isDescending)[] selectorsAndDirections)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selectorsAndDirections);
#else
        if (selectorsAndDirections is null)
            throw new ArgumentNullException(nameof(selectorsAndDirections));
#endif
        ObservableCollectionOrderByQuery<TElement> orderByQuery;
        var key = selectorsAndDirections.ToList().AsReadOnly();
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = key.Select(selectorAndDirection => ((Expression<Func<TElement, IComparable>>)optimizer(selectorAndDirection.selector), selectorAndDirection.isDescending)).ToList().AsReadOnly();
        lock (cachedOrderByQueriesAccess)
        {
            if (!cachedOrderByQueries.TryGetValue(key, out orderByQuery!))
            {
                orderByQuery = new ObservableCollectionOrderByQuery<TElement>(collectionObserver, this, selectorsAndDirections);
                cachedOrderByQueries.Add(key, orderByQuery);
            }
            ++orderByQuery.Observations;
        }
        orderByQuery.Initialize();
        return orderByQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveSelect<TResult>(Expression<Func<TElement, TResult>> selector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        ObservableQuery selectQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (cachedSelectQueriesAccess)
        {
            if (!cachedSelectQueries.TryGetValue(key, out selectQuery!))
            {
                selectQuery = new ObservableCollectionSelectQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSelectQueries.Add(key, selectQuery);
            }
            ++selectQuery.Observations;
        }
        selectQuery.Initialize();
        return (ObservableCollectionSelectQuery<TElement, TResult>)selectQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveSelectMany<TResult>(Expression<Func<TElement, IEnumerable<TResult>>> selector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        ObservableQuery selectManyQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, IEnumerable<TResult>>>)optimizer(key);
        lock (cachedSelectManyQueriesAccess)
        {
            if (!cachedSelectManyQueries.TryGetValue(key, out selectManyQuery!))
            {
                selectManyQuery = new ObservableCollectionSelectManyQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSelectManyQueries.Add(key, selectManyQuery);
            }
            ++selectManyQuery.Observations;
        }
        selectManyQuery.Initialize();
        return (ObservableCollectionSelectManyQuery<TElement, TResult>)selectManyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingle() =>
        ObserveIndex(null, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingle(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var single = where.ObserveSingle();
        single.Disposed += (_, _) => where.Dispose();
        return single;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingleOrDefault() =>
        ObserveIndex(null, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingleOrDefault(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var where = ObserveWhere(predicate);
        var singleOrDefault = where.ObserveSingleOrDefault();
        singleOrDefault.Disposed += (_, _) => where.Dispose();
        return singleOrDefault;
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSum() =>
        ObserveSum(element => element);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TElement, TResult>> selector)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(selector);
#else
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
#endif
        ObservableQuery sumQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (cachedSumQueriesAccess)
        {
            if (!cachedSumQueries.TryGetValue(key, out sumQuery!))
            {
                sumQuery = new ObservableCollectionSumQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSumQueries.Add(key, sumQuery);
            }
            ++sumQuery.Observations;
        }
        sumQuery.Initialize();
        return (ObservableCollectionSumQuery<TElement, TResult>)sumQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, element => element, equalityComparer);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, valueSelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull
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
        ObservableQuery toDictionaryQuery;
        var key = (keySelector, valueSelector, equalityComparer);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = ((Expression<Func<TElement, TKey>>)optimizer(keySelector), (Expression<Func<TElement, TValue>>)optimizer(valueSelector), equalityComparer);
        lock (cachedToDictionaryQueriesAccess)
        {
            if (!cachedToDictionaryQueries.TryGetValue(key, out toDictionaryQuery!))
            {
                toDictionaryQuery = new ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(collectionObserver, this, key.keySelector, key.valueSelector, key.equalityComparer);
                cachedToDictionaryQueries.Add(key, toDictionaryQuery);
            }
            ++toDictionaryQuery.Observations;
        }
        toDictionaryQuery.Initialize();
        return (ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>)toDictionaryQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallback(object context, CollectionSynchronizationCallback synchronizationCallback)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(synchronizationCallback);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (synchronizationCallback is null)
            throw new ArgumentNullException(nameof(synchronizationCallback));
#endif
        ObservableCollectionUsingSynchronizationCallbackQuery<TElement> usingSynchronizationCallbackQuery;
        var key = (context, synchronizationCallback);
        lock (cachedUsingSynchronizationCallbackQueriesAccess)
        {
            if (!cachedUsingSynchronizationCallbackQueries.TryGetValue(key, out usingSynchronizationCallbackQuery!))
            {
                usingSynchronizationCallbackQuery = new ObservableCollectionUsingSynchronizationCallbackQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                cachedUsingSynchronizationCallbackQueries.Add(key, usingSynchronizationCallbackQuery);
            }
            ++usingSynchronizationCallbackQuery.Observations;
        }
        usingSynchronizationCallbackQuery.Initialize();
        return usingSynchronizationCallbackQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallbackEventually(object context, CollectionSynchronizationCallback synchronizationCallback)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(synchronizationCallback);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (synchronizationCallback is null)
            throw new ArgumentNullException(nameof(synchronizationCallback));
#endif
        ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> usingSynchronizationCallbackEventuallyQuery;
        var key = (context, synchronizationCallback);
        lock (cachedUsingSynchronizationCallbackEventuallyQueriesAccess)
        {
            if (!cachedUsingSynchronizationCallbackEventuallyQueries.TryGetValue(key, out usingSynchronizationCallbackEventuallyQuery!))
            {
                usingSynchronizationCallbackEventuallyQuery = new ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                cachedUsingSynchronizationCallbackEventuallyQueries.Add(key, usingSynchronizationCallbackEventuallyQuery);
            }
            ++usingSynchronizationCallbackEventuallyQuery.Observations;
        }
        usingSynchronizationCallbackEventuallyQuery.Initialize();
        return usingSynchronizationCallbackEventuallyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(synchronizationContext);
#else
        if (synchronizationContext is null)
            throw new ArgumentNullException(nameof(synchronizationContext));
#endif
        ObservableCollectionUsingSynchronizationContextQuery<TElement> usingSynchronizationContextQuery;
        lock (cachedUsingSynchronizationContextQueriesAccess)
        {
            if (!cachedUsingSynchronizationContextQueries.TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                usingSynchronizationContextQuery = new ObservableCollectionUsingSynchronizationContextQuery<TElement>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextQueries.Add(synchronizationContext, usingSynchronizationContextQuery);
            }
            ++usingSynchronizationContextQuery.Observations;
        }
        usingSynchronizationContextQuery.Initialize();
        return usingSynchronizationContextQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(synchronizationContext);
#else
        if (synchronizationContext is null)
            throw new ArgumentNullException(nameof(synchronizationContext));
#endif
        ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> usingSynchronizationContextEventuallyQuery;
        lock (cachedUsingSynchronizationContextEventuallyQueriesAccess)
        {
            if (!cachedUsingSynchronizationContextEventuallyQueries.TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                usingSynchronizationContextEventuallyQuery = new ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextEventuallyQueries.Add(synchronizationContext, usingSynchronizationContextEventuallyQuery);
            }
            ++usingSynchronizationContextEventuallyQuery.Observations;
        }
        usingSynchronizationContextEventuallyQuery.Initialize();
        return usingSynchronizationContextEventuallyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSyncRoot(object lockObject)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(lockObject);
#else
        if (lockObject is null)
            throw new ArgumentNullException(nameof(lockObject));
#endif
        ObservableCollectionUsingSyncRootQuery<TElement> usingSyncRootQuery;
        lock (cachedUsingSyncRootQueriesAccess)
        {
            if (!cachedUsingSyncRootQueries.TryGetValue(lockObject, out usingSyncRootQuery!))
            {
                usingSyncRootQuery = new ObservableCollectionUsingSyncRootQuery<TElement>(collectionObserver, this, lockObject);
                cachedUsingSyncRootQueries.Add(lockObject, usingSyncRootQuery);
            }
            ++usingSyncRootQuery.Observations;
        }
        usingSyncRootQuery.Initialize();
        return usingSyncRootQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSyncRootEventually(object lockObject)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(lockObject);
#else
        if (lockObject is null)
            throw new ArgumentNullException(nameof(lockObject));
#endif
        ObservableCollectionUsingSyncRootEventuallyQuery<TElement> usingSyncRootEventuallyQuery;
        lock (cachedUsingSyncRootEventuallyQueriesAccess)
        {
            if (!cachedUsingSyncRootEventuallyQueries.TryGetValue(lockObject, out usingSyncRootEventuallyQuery!))
            {
                usingSyncRootEventuallyQuery = new ObservableCollectionUsingSyncRootEventuallyQuery<TElement>(collectionObserver, this, lockObject);
                cachedUsingSyncRootEventuallyQueries.Add(lockObject, usingSyncRootEventuallyQuery);
            }
            ++usingSyncRootEventuallyQuery.Observations;
        }
        usingSyncRootEventuallyQuery.Initialize();
        return usingSyncRootEventuallyQuery;
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveWhere(Expression<Func<TElement, bool>> predicate)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        ObservableCollectionWhereQuery<TElement> whereQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (cachedWhereQueriesAccess)
        {
            if (!cachedWhereQueries.TryGetValue(key, out whereQuery!))
            {
                whereQuery = new ObservableCollectionWhereQuery<TElement>(collectionObserver, this, key);
                cachedWhereQueries.Add(key, whereQuery);
            }
            ++whereQuery.Observations;
        }
        whereQuery.Initialize();
        return whereQuery;
    }

    internal bool QueryDisposed<TAccumulate, TResult>(ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult> aggregateQuery)
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

    internal bool QueryDisposed(ObservableCollectionAllQuery<TElement> allQuery)
    {
        lock (cachedAllQueriesAccess)
        {
            if (--allQuery.Observations == 0)
            {
                cachedAllQueries.Remove(allQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAnyQuery<TElement> anyQuery)
    {
        lock (cachedAnyQueriesAccess)
        {
            if (--anyQuery.Observations == 0)
            {
                cachedAnyQueries.Remove(anyQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionAverageQuery<TElement, TResult> averageQuery)
    {
        lock (cachedAverageQueriesAccess)
        {
            if (--averageQuery.Observations == 0)
            {
                cachedAverageQueries.Remove(averageQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionComparisonQuery<TElement> comparisonQuery)
    {
        lock (cachedComparisonQueriesAccess)
        {
            if (--comparisonQuery.Observations == 0)
            {
                cachedComparisonQueries.Remove(comparisonQuery.SoughtComparison);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionConcatQuery<TElement> concatQuery)
    {
        lock (cachedConcatQueriesAccess)
        {
            if (--concatQuery.Observations == 0)
            {
                cachedConcatQueries.Remove(concatQuery.Second);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey>(ObservableCollectionGroupByQuery<TKey, TElement> groupByQuery)
    {
        lock (cachedGroupByQueriesAccess)
        {
            if (--groupByQuery.Observations == 0)
            {
                cachedGroupByQueries.Remove((groupByQuery.KeySelector, groupByQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndexQuery<TElement> indexQuery)
    {
        lock (cachedIndexQueriesAccess)
        {
            if (--indexQuery.Observations == 0)
            {
                cachedIndexQueries.Remove((indexQuery.Index, indexQuery.OutOfRangeIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndividualChangesQuery<TElement> individualChangesQuery)
    {
        lock (cachedIndividualChangeQueryAccess)
        {
            if (--individualChangesQuery.Observations == 0)
            {
                cachedIndividualChangeQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionOrderByQuery<TElement> orderByQuery)
    {
        lock (cachedOrderByQueriesAccess)
        {
            if (--orderByQuery.Observations == 0)
            {
                cachedOrderByQueries.Remove(orderByQuery.SelectorsAndDirections);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSelectQuery<TElement, TResult> selectQuery)
    {
        lock (cachedSelectQueriesAccess)
        {
            if (--selectQuery.Observations == 0)
            {
                cachedSelectQueries.Remove(selectQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSelectManyQuery<TElement, TResult> selectManyQuery)
    {
        lock (cachedSelectManyQueriesAccess)
        {
            if (--selectManyQuery.Observations == 0)
            {
                cachedSelectManyQueries.Remove(selectManyQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSumQuery<TElement, TResult> sumQuery)
    {
        lock (cachedSumQueriesAccess)
        {
            if (--sumQuery.Observations == 0)
            {
                cachedSumQueries.Remove(sumQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey, TValue>(ObservableCollectionToDictionaryQuery<TElement, TKey, TValue> toDictionaryQuery)
        where TKey : notnull
    {
        lock (cachedToDictionaryQueriesAccess)
        {
            if (--toDictionaryQuery.Observations == 0)
            {
                cachedToDictionaryQueries.Remove((toDictionaryQuery.KeySelector, toDictionaryQuery.ValueSelector, toDictionaryQuery.EqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> usingSynchronizationCallbackEventuallyQuery)
    {
        lock (cachedUsingSynchronizationCallbackEventuallyQueriesAccess)
        {
            if (--usingSynchronizationCallbackEventuallyQuery.Observations == 0)
            {
                cachedUsingSynchronizationCallbackEventuallyQueries.Remove((usingSynchronizationCallbackEventuallyQuery.Context, usingSynchronizationCallbackEventuallyQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackQuery<TElement> usingSynchronizationCallbackQuery)
    {
        lock (cachedUsingSynchronizationCallbackQueriesAccess)
        {
            if (--usingSynchronizationCallbackQuery.Observations == 0)
            {
                cachedUsingSynchronizationCallbackQueries.Remove((usingSynchronizationCallbackQuery.Context, usingSynchronizationCallbackQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> usingSynchronizationContextEventuallyQuery)
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

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextQuery<TElement> usingSynchronizationContextQuery)
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

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootEventuallyQuery<TElement> usingSyncRootEventuallyQuery)
    {
        lock (cachedUsingSyncRootEventuallyQueriesAccess)
        {
            if (--usingSyncRootEventuallyQuery.Observations == 0)
            {
                cachedUsingSyncRootEventuallyQueries.Remove(usingSyncRootEventuallyQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootQuery<TElement> usingSyncRootQuery)
    {
        lock (cachedUsingSyncRootQueriesAccess)
        {
            if (--usingSyncRootQuery.Observations == 0)
            {
                cachedUsingSyncRootQueries.Remove(usingSyncRootQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionWhereQuery<TElement> whereQuery)
    {
        lock (cachedWhereQueriesAccess)
        {
            if (--whereQuery.Observations == 0)
            {
                cachedWhereQueries.Remove(whereQuery.Predicate);
                return true;
            }
        }
        return false;
    }
}
