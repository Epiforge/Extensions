namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableCollectionQuery<TElement>(CollectionObserver collectionObserver) :
    ObservableQuery(collectionObserver),
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
            var hashCode = new System.HashCode();
            hashCode.Add(ExpressionEqualityComparer.Default.GetHashCode(obj.keySelector));
            hashCode.Add(obj.keyEqualityComaprer?.GetHashCode() ?? 0);
            return hashCode.ToHashCode();
        }
    }

    sealed class CachedLookupQueryEqualityComparer :
        IEqualityComparer<(Expression keySelector, object keyEqualityComaprer)>
    {
        public static CachedLookupQueryEqualityComparer Default { get; } = new();

        public bool Equals((Expression keySelector, object keyEqualityComaprer) x, (Expression keySelector, object keyEqualityComaprer) y) =>
            ExpressionEqualityComparer.Default.Equals(x.keySelector, y.keySelector) && ReferenceEquals(x.keyEqualityComaprer, y.keyEqualityComaprer);

        public int GetHashCode([DisallowNull] (Expression keySelector, object keyEqualityComaprer) obj)
        {
            var hashCode = new System.HashCode();
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
            var hashCode = new System.HashCode();
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
            var hashCode = new System.HashCode();
            var expressionEqualityComparer = ExpressionEqualityComparer.Default;
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.keySelector));
            hashCode.Add(expressionEqualityComparer.GetHashCode(obj.valueSelector));
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
        internal static readonly Expression<Func<TElement, bool>> IsResult = element => element is TResult;
        internal static readonly Expression<Func<TElement, TResult>> Selector = element => (TResult)(object)element!;
    }

    /// <summary>
    /// Holds the lambdas selecting an element and the key of a grouping of elements by themselves, built once for each element type, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class IdentityLambdas
    {
        internal static readonly Expression<Func<IObservableGrouping<TElement, TElement>, TElement>> GroupingKey = grouping => grouping.Key;
        internal static readonly Expression<Func<TElement, TElement>> Selector = element => element;
    }

    /// <summary>
    /// Holds the lambda pairing an element with the key a key selector selects, built once for each key selector and kept for as long as its caller keeps the key selector, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    static class KeyPairings<TKey>
    {
        static readonly ConditionalWeakTable<Expression<Func<TElement, TKey>>, Expression<Func<TElement, (TElement, TKey)>>> byKeySelector = [];

        internal static Expression<Func<TElement, (TElement, TKey)>> For(Expression<Func<TElement, TKey>> keySelector) =>
            byKeySelector.GetValue(keySelector, static source =>
            {
                var parameter = Expression.Parameter(typeof(TElement), "element");
                return Expression.Lambda<Func<TElement, (TElement, TKey)>>(Expression.New(typeof((TElement, TKey)).GetConstructor([typeof(TElement), typeof(TKey)])!, parameter, LambdaInvocationRewriter.Apply(source, parameter) ?? Expression.Invoke(source, parameter)), parameter);
            });
    }

    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery>? cachedAggregateQueries;
    Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionAllQuery<TElement>>? cachedAllQueries;
    NullableKeyDictionary<Expression<Func<TElement, bool>>?, ObservableCollectionAnyQuery<TElement>>? cachedAnyQueries;
    NullableKeyDictionary<TElement, ObservableCollectionAppendQuery<TElement>>? cachedAppendQueries;
    Dictionary<Expression, ObservableQuery>? cachedAverageQueries;
    Dictionary<(int soughtComparison, IComparer<TElement> comparer), ObservableCollectionComparisonQuery<TElement>>? cachedComparisonQueries;
    Dictionary<IObservableCollectionQuery<TElement>, ObservableCollectionConcatQuery<TElement>>? cachedConcatQueries;
    ObservableCollectionCountQuery<TElement>? cachedCountQuery;
    Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery>? cachedGroupByQueries;
    Dictionary<Func<int, int>, ObservableCollectionIndexForCountQuery<TElement>>? cachedIndexForCountQueries;
    Dictionary<(Index? index, bool outOfRangeIsDefault), ObservableQuery>? cachedIndexQueries;
    ObservableCollectionIndividualChangesQuery<TElement>? cachedIndividualChangeQuery;
    Dictionary<IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>, ObservableCollectionOrderByQuery<TElement>>? cachedOrderByQueries;
    NullableKeyDictionary<TElement, ObservableCollectionPrependQuery<TElement>>? cachedPrependQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectManyQueries;
    Dictionary<Range, ObservableCollectionSliceQuery<TElement>>? cachedSliceQueries;
    Dictionary<Expression, ObservableQuery>? cachedSumQueries;
    Dictionary<(Expression keySelector, Expression valueSelector, object equalityComparer), ObservableQuery>? cachedToDictionaryQueries;
    Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery>? cachedToLookupQueries;
    Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>>? cachedUsingSynchronizationCallbackEventuallyQueries;
    Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackQuery<TElement>>? cachedUsingSynchronizationCallbackQueries;
    Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>>? cachedUsingSynchronizationContextEventuallyQueries;
    Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextQuery<TElement>>? cachedUsingSynchronizationContextQueries;
    Dictionary<object, ObservableCollectionUsingSyncRootEventuallyQuery<TElement>>? cachedUsingSyncRootEventuallyQueries;
    Dictionary<object, ObservableCollectionUsingSyncRootQuery<TElement>>? cachedUsingSyncRootQueries;
    Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionWhereQuery<TElement>>? cachedWhereQueries;
#if IS_NET_9_0_OR_GREATER
    Lock? cachedQueriesAccess;
#else
    object? cachedQueriesAccess;
#endif
    Exception? operationFault;

    public abstract TElement this[int index] { get; }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    TElement IList<TElement>.this[int index]
    {
        get => this[index];
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
                count += cachedAppendQueries?.Values.Sum(appendQuery => 1 + appendQuery.CachedObservableQueries) ?? 0;
                count += cachedAverageQueries?.Values.Sum(averageQuery => 1 + averageQuery.CachedObservableQueries) ?? 0;
                count += cachedComparisonQueries?.Values.Sum(comparerQuery => 1 + comparerQuery.CachedObservableQueries) ?? 0;
                count += cachedConcatQueries?.Values.Sum(concatQuery => 1 + concatQuery.CachedObservableQueries) ?? 0;
                count += cachedCountQuery is null ? 0 : 1 + cachedCountQuery.CachedObservableQueries;
                count += cachedGroupByQueries?.Values.Sum(groupByQuery => 1 + groupByQuery.CachedObservableQueries) ?? 0;
                count += cachedIndexForCountQueries?.Values.Sum(indexForCountQuery => 1 + indexForCountQuery.CachedObservableQueries) ?? 0;
                count += cachedIndexQueries?.Values.Sum(indexQuery => 1 + indexQuery.CachedObservableQueries) ?? 0;
                count += cachedIndividualChangeQuery is null ? 0 : 1 + cachedIndividualChangeQuery.CachedObservableQueries;
                count += cachedOrderByQueries?.Values.Sum(orderByQuery => 1 + orderByQuery.CachedObservableQueries) ?? 0;
                count += cachedPrependQueries?.Values.Sum(prependQuery => 1 + prependQuery.CachedObservableQueries) ?? 0;
                count += cachedSelectQueries?.Values.Sum(selectQuery => 1 + selectQuery.CachedObservableQueries) ?? 0;
                count += cachedSelectManyQueries?.Values.Sum(selectManyQuery => 1 + selectManyQuery.CachedObservableQueries) ?? 0;
                count += cachedSliceQueries?.Values.Sum(sliceQuery => 1 + sliceQuery.CachedObservableQueries) ?? 0;
                count += cachedSumQueries?.Values.Sum(sumQuery => 1 + sumQuery.CachedObservableQueries) ?? 0;
                count += cachedToDictionaryQueries?.Values.Sum(toDictionaryQuery => 1 + toDictionaryQuery.CachedObservableQueries) ?? 0;
                count += cachedToLookupQueries?.Values.Sum(toLookupQuery => 1 + toLookupQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationCallbackEventuallyQueries?.Values.Sum(usingSynchronizationCallbackEventuallyQuery => 1 + usingSynchronizationCallbackEventuallyQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationCallbackQueries?.Values.Sum(usingSynchronizationCallbackQuery => 1 + usingSynchronizationCallbackQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationContextEventuallyQueries?.Values.Sum(usingSynchronizationContextEventuallyQuery => 1 + usingSynchronizationContextEventuallyQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSynchronizationContextQueries?.Values.Sum(usingSynchronizationContextQuery => 1 + usingSynchronizationContextQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSyncRootEventuallyQueries?.Values.Sum(usingSyncRootEventuallyQuery => 1 + usingSyncRootEventuallyQuery.CachedObservableQueries) ?? 0;
                count += cachedUsingSyncRootQueries?.Values.Sum(usingSyncRootQuery => 1 + usingSyncRootQuery.CachedObservableQueries) ?? 0;
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
        throw new NotSupportedException("The SyncRoot property may not be used for the synchronization of observable queries; use ObserveUsingSyncRoot instead.");

    internal IObservableCollectionQuery<TElement> AsScoped() =>
        new ScopedObservableCollectionQuery<TElement>(this);

    bool IList.IsFixedSize =>
        false;

    bool IList.IsReadOnly =>
        true;

    bool ICollection<TElement>.IsReadOnly =>
        true;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public virtual bool Contains(TElement item)
    {
        if (!HasIndexerPenalty)
            for (int i = 0, ii = Count; i < ii; ++i)
            {
                var element = this[i];
                if (EqualityComparer<TElement>.Default.Equals(element, item))
                    return true;
            }
        else
            foreach (var element in this)
                if (EqualityComparer<TElement>.Default.Equals(element, item))
                    return true;
        return false;
    }

    bool IList.Contains(object? value) =>
        value is TElement element && Contains(element);

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

    public void CopyTo(TElement[] array, int arrayIndex) =>
        CopyTo((Array)array, arrayIndex);

    public abstract IEnumerator<TElement> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public IReadOnlyList<TElement> GetRange(int index, int count)
    {
        var list = new List<TElement>();
        if (!HasIndexerPenalty)
            for (int i = index, ii = Math.Min(index + count, Count); i < ii; ++i)
                list.Add(this[i]);
        else
        {
            var enumerating = -1;
            foreach (var item in this)
            {
                ++enumerating;
                if (enumerating < index)
                    continue;
                if (list.Count >= count)
                    break;
                list.Add(item);
            }
        }
        return list.AsReadOnly();
    }

    public int IndexOf(TElement item)
    {
        if (!HasIndexerPenalty)
        {
            for (int i = 0, ii = Count; i < ii; ++i)
                if (EqualityComparer<TElement>.Default.Equals(this[i], item))
                    return i;
        }
        else
        {
            var enumerating = -1;
            foreach (var element in this)
            {
                ++enumerating;
                if (EqualityComparer<TElement>.Default.Equals(element, item))
                    return enumerating;
            }
        }
        return -1;
    }

    int IList.IndexOf(object? value) =>
        value is TElement element ? IndexOf(element) : -1;

    private protected bool IsChangeObserved =>
        CollectionChanged is not null;

    protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!DeferNotification(e))
            RaiseCollectionChanged(e);
    }

    void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        var eventArgs = Logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        CollectionChanged?.Invoke(this, e);
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    private protected override void RaiseNotification(object eventArguments)
    {
        if (eventArguments is NotifyCollectionChangedEventArgs collectionChangedEventArgs)
            RaiseCollectionChanged(collectionChangedEventArgs);
        else
            base.RaiseNotification(eventArguments);
    }

    #region Unsupported Operations

    int IList.Add(object? value) =>
        throw new NotSupportedException();

    void ICollection<TElement>.Add(TElement item) =>
        throw new NotSupportedException();

    void IList.Clear() =>
        throw new NotSupportedException();

    void ICollection<TElement>.Clear() =>
        throw new NotSupportedException();

    void IList.Insert(int index, object? value) =>
        throw new NotSupportedException();

    void IList<TElement>.Insert(int index, TElement item) =>
        throw new NotSupportedException();

    void IList.Remove(object? value) =>
        throw new NotSupportedException();

    bool ICollection<TElement>.Remove(TElement item) =>
        throw new NotSupportedException();

    void IList.RemoveAt(int index) =>
        throw new NotSupportedException();

    void IList<TElement>.RemoveAt(int index) =>
        throw new NotSupportedException();

    #endregion Unsupported Operations

    #region Observation Methods

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(seedFactory);
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(resultSelector);
        ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult> aggregateQuery;
        lock (CachedQueriesAccess)
        {
            var key = (seedFactory, func, resultSelector);
            if (!(cachedAggregateQueries ??= []).TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = new ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
                cachedAggregateQueries.Add(key, aggregateQuery);
            }
            else
                aggregateQuery = (ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>)cachedAggregateQuery;
            ++aggregateQuery.Observations;
        }
        aggregateQuery.Initialize();
        return aggregateQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObservableCollectionAllQuery<TElement> allQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedAllQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out allQuery!))
            {
                allQuery = new ObservableCollectionAllQuery<TElement>(collectionObserver, this, key);
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
        ObservableCollectionAnyQuery<TElement> anyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedAnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(null, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this);
                cachedAnyQueries.Add(null, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObservableCollectionAnyQuery<TElement> anyQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedAnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(key, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this, key);
                cachedAnyQueries.Add(key, anyQuery);
            }
            ++anyQuery.Observations;
        }
        anyQuery.Initialize();
        return anyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveAverage() =>
        ObserveAverage(IdentityLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveAppend(TElement element)
    {
        ObservableCollectionAppendQuery<TElement> appendQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedAppendQueries ??= []).TryGetValue(element, out appendQuery!))
            {
                appendQuery = new ObservableCollectionAppendQuery<TElement>(collectionObserver, this, element);
                cachedAppendQueries.Add(element, appendQuery);
            }
            ++appendQuery.Observations;
        }
        appendQuery.Initialize();
        return appendQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TElement, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObservableQuery averageQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedAverageQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out averageQuery!))
            {
                averageQuery = new ObservableCollectionAverageQuery<TElement, TResult>(collectionObserver, this, key);
                cachedAverageQueries.Add(key, averageQuery);
            }
            ++averageQuery.Observations;
        }
        averageQuery.Initialize();
        return ((ObservableCollectionAverageQuery<TElement, TResult>)averageQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveCast<TResult>() =>
        ObserveSelect(CastLambdas<TResult>.Selector);

    [return: DisposeWhenDiscarded]
    internal IObservableScalarQuery<TElement> ObserveComparison(int soughtComparison, IComparer<TElement> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ObservableCollectionComparisonQuery<TElement> comparisonQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedComparisonQueries ??= []).TryGetValue((soughtComparison, comparer), out comparisonQuery!))
            {
                comparisonQuery = new ObservableCollectionComparisonQuery<TElement>(collectionObserver, this, soughtComparison, comparer);
                cachedComparisonQueries.Add((soughtComparison, comparer), comparisonQuery);
            }
            ++comparisonQuery.Observations;
        }
        comparisonQuery.Initialize();
        return comparisonQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveComparison<TResult>(Expression<Func<TElement, TResult>> selector, int soughtComparison, IComparer<TResult> comparer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(comparer);
        var select = ObserveSelect(selector);
        try
        {
            var comparison = ((ScopedObservableCollectionQuery<TResult>)select).query.ObserveComparison(soughtComparison, comparer);
            comparison.Disposed += (_, _) => select.Dispose();
            return comparison;
        }
        catch
        {
            select.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Pairs each element with its key, seeks the pair whose key compares as sought, and returns the element of that pair
    /// </summary>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveComparisonBy<TKey>(Expression<Func<TElement, TKey>> keySelector, int soughtComparison, IComparer<TKey> comparer)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(comparer);
        var comparison = ObserveComparison(KeyPairings<TKey>.For(keySelector), soughtComparison, new ObservableCollectionKeyComparer<TElement, TKey>(comparer));
        try
        {
            var element = comparison.ObserveTransform(ObservableCollectionKeyComparer<TElement, TKey>.ElementOf);
            element.Disposed += (_, _) => comparison.Dispose();
            return element;
        }
        catch
        {
            comparison.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveConcat(IObservableCollectionQuery<TElement> second)
    {
        ArgumentNullException.ThrowIfNull(second);
        if (second is ScopedObservableCollectionQuery<TElement> scopedSecond)
            second = scopedSecond.query;
        ObservableCollectionConcatQuery<TElement> concatQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedConcatQueries ??= []).TryGetValue(second, out concatQuery!))
            {
                concatQuery = new ObservableCollectionConcatQuery<TElement>(collectionObserver, this, second);
                cachedConcatQueries.Add(second, concatQuery);
            }
            ++concatQuery.Observations;
        }
        concatQuery.Initialize();
        return concatQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount()
    {
        lock (CachedQueriesAccess)
        {
            cachedCountQuery ??= new ObservableCollectionCountQuery<TElement>(collectionObserver, this);
            ++cachedCountQuery.Observations;
        }
        cachedCountQuery.Initialize();
        return cachedCountQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount(Expression<Func<TElement, bool>> predicate)
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
    public IObservableCollectionQuery<TElement> ObserveDistinct() =>
        ObserveDistinct(EqualityComparer<TElement>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveDistinct(IEqualityComparer<TElement> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        var groupBy = ObserveGroupBy(IdentityLambdas.Selector, comparer);
        try
        {
            var select = groupBy.ObserveSelect(IdentityLambdas.GroupingKey);
            select.Disposed += (_, _) => groupBy.Dispose();
            return select;
        }
        catch
        {
            groupBy.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveElementAt(int index) =>
        ObserveIndex(new Index(Math.Abs(index), index < 0), false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveElementAt(Func<int, int> indexForCount)
    {
        ArgumentNullException.ThrowIfNull(indexForCount);
        ObservableCollectionIndexForCountQuery<TElement> indexForCountQuery;
        lock (CachedQueriesAccess)
        {
            cachedIndexForCountQueries ??= [];
            if (!cachedIndexForCountQueries.TryGetValue(indexForCount, out indexForCountQuery!))
            {
                indexForCountQuery = new ObservableCollectionIndexForCountQuery<TElement>(collectionObserver, this, indexForCount);
                cachedIndexForCountQueries.Add(indexForCount, indexForCountQuery);
            }
            ++indexForCountQuery.Observations;
        }
        indexForCountQuery.Initialize();
        return indexForCountQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveElementAtOrDefault(int index) =>
        ObserveIndex(new Index(Math.Abs(index), index < 0), true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirst() =>
        ObserveIndex(0, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirst(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var where = ObserveWhere(predicate);
        try
        {
            var first = where.ObserveFirst();
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
    public IObservableScalarQuery<TElement> ObserveFirstOrDefault() =>
        ObserveIndex(0, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveFirstOrDefault(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var where = ObserveWhere(predicate);
        try
        {
            var firstOrDefault = where.ObserveFirstOrDefault();
            firstOrDefault.Disposed += (_, _) => where.Dispose();
            return firstOrDefault;
        }
        catch
        {
            where.Dispose();
            throw;
        }
    }

    IObservableScalarQuery<TElement> ObserveIndex(Index? index, bool outOfRangeIsDefault)
    {
        ObservableCollectionIndexQuery<TElement> indexQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedIndexQueries ??= []).TryGetValue((index, outOfRangeIsDefault), out var cachedIndexQuery))
            {
                indexQuery = new ObservableCollectionIndexQuery<TElement>(collectionObserver, this, index, outOfRangeIsDefault);
                cachedIndexQueries.Add((index, outOfRangeIsDefault), indexQuery);
            }
            else
                indexQuery = (ObservableCollectionIndexQuery<TElement>)cachedIndexQuery;
            ++indexQuery.Observations;
        }
        indexQuery.Initialize();
        return indexQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveIndividualChanges()
    {
        lock (CachedQueriesAccess)
        {
            cachedIndividualChangeQuery ??= new ObservableCollectionIndividualChangesQuery<TElement>(collectionObserver, this);
            ++cachedIndividualChangeQuery.Observations;
        }
        cachedIndividualChangeQuery.Initialize();
        return cachedIndividualChangeQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector) =>
        ObserveGroupBy(keySelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(keyEqualityComparer);
        ObservableQuery groupByQuery;
        var optimizedKeySelector = keySelector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            optimizedKeySelector = (Expression<Func<TElement, TKey>>)optimizer(optimizedKeySelector);
        var key = (optimizedKeySelector, keyEqualityComparer);
        lock (CachedQueriesAccess)
        {
            if (!(cachedGroupByQueries ??= new(CachedGroupByQueryEqualityComparer.Default)).TryGetValue(key, out groupByQuery!))
            {
                groupByQuery = new ObservableCollectionGroupByQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
                cachedGroupByQueries.Add(key, groupByQuery);
            }
            ++groupByQuery.Observations;
        }
        groupByQuery.Initialize();
        return ((ObservableCollectionGroupByQuery<TKey, TElement>)groupByQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLast() =>
        ObserveIndex(^1, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLast(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var where = ObserveWhere(predicate);
        try
        {
            var last = where.ObserveLast();
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
    public IObservableScalarQuery<TElement> ObserveLastOrDefault() =>
        ObserveIndex(^1, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveLastOrDefault(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var where = ObserveWhere(predicate);
        try
        {
            var lastOrDefault = where.ObserveLastOrDefault();
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
    public IObservableScalarQuery<TElement> ObserveMax() =>
        ObserveComparison(1, Comparer<TElement>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMax(IComparer<TElement> comparer) =>
        ObserveComparison(1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TElement, TResult>> selector) =>
        ObserveComparison(selector, 1, Comparer<TResult>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TElement, TResult>> selector, IComparer<TResult> comparer) =>
        ObserveComparison(selector, 1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMaxBy<TKey>(Expression<Func<TElement, TKey>> keySelector) =>
        ObserveComparisonBy(keySelector, 1, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMaxBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IComparer<TKey> comparer) =>
        ObserveComparisonBy(keySelector, 1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMin() =>
        ObserveComparison(-1, Comparer<TElement>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMin(IComparer<TElement> comparer) =>
        ObserveComparison(-1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TElement, TResult>> selector) =>
        ObserveComparison(selector, -1, Comparer<TResult>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TElement, TResult>> selector, IComparer<TResult> comparer) =>
        ObserveComparison(selector, -1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMinBy<TKey>(Expression<Func<TElement, TKey>> keySelector) =>
        ObserveComparisonBy(keySelector, -1, Comparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveMinBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IComparer<TKey> comparer) =>
        ObserveComparisonBy(keySelector, -1, comparer);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveOfType<TResult>()
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
    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector) =>
        ObserveOrderBy(selector, false);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector, bool isDescending) =>
        ObserveOrderBy((selector, isDescending));

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveOrderBy(params (Expression<Func<TElement, IComparable>> selector, bool isDescending)[] selectorsAndDirections)
    {
        ArgumentNullException.ThrowIfNull(selectorsAndDirections);
        ObservableCollectionOrderByQuery<TElement> orderByQuery;
        var key = selectorsAndDirections.ToList().AsReadOnly();
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = key.Select(selectorAndDirection => ((Expression<Func<TElement, IComparable>>)optimizer(selectorAndDirection.selector), selectorAndDirection.isDescending)).ToList().AsReadOnly();
        lock (CachedQueriesAccess)
        {
            if (!(cachedOrderByQueries ??= new(CachedOrderByQueryEqualityComparer.Default)).TryGetValue(key, out orderByQuery!))
            {
                orderByQuery = new ObservableCollectionOrderByQuery<TElement>(collectionObserver, this, selectorsAndDirections);
                cachedOrderByQueries.Add(key, orderByQuery);
            }
            ++orderByQuery.Observations;
        }
        orderByQuery.Initialize();
        return orderByQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObservePrepend(TElement element)
    {
        ObservableCollectionPrependQuery<TElement> prependQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedPrependQueries ??= []).TryGetValue(element, out prependQuery!))
            {
                prependQuery = new ObservableCollectionPrependQuery<TElement>(collectionObserver, this, element);
                cachedPrependQueries.Add(element, prependQuery);
            }
            ++prependQuery.Observations;
        }
        prependQuery.Initialize();
        return prependQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<(int Ahead, int Tied)> ObserveRank(TElement element, Expression<Func<TElement, IComparable>> selector, bool isDescending) =>
        ObserveRank(element, (selector, isDescending));

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<(int Ahead, int Tied)> ObserveRank(TElement element, params (Expression<Func<TElement, IComparable>> selector, bool isDescending)[] selectorsAndDirections)
    {
        var orderBy = ObserveOrderBy(selectorsAndDirections);
        try
        {
            var rank = ((ObservableCollectionOrderByQuery<TElement>)((ScopedObservableCollectionQuery<TElement>)orderBy).query).ObserveRank(element);
            rank.Disposed += (_, _) => orderBy.Dispose();
            return rank;
        }
        catch
        {
            orderBy.Dispose();
            throw;
        }
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveSelect<TResult>(Expression<Func<TElement, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObservableQuery selectQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedSelectQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out selectQuery!))
            {
                selectQuery = new ObservableCollectionSelectQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSelectQueries.Add(key, selectQuery);
            }
            ++selectQuery.Observations;
        }
        selectQuery.Initialize();
        return ((ObservableCollectionSelectQuery<TElement, TResult>)selectQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveSelectMany<TResult>(Expression<Func<TElement, IEnumerable<TResult>>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObservableQuery selectManyQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, IEnumerable<TResult>>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedSelectManyQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out selectManyQuery!))
            {
                selectManyQuery = new ObservableCollectionSelectManyQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSelectManyQueries.Add(key, selectManyQuery);
            }
            ++selectManyQuery.Observations;
        }
        selectManyQuery.Initialize();
        return ((ObservableCollectionSelectManyQuery<TElement, TResult>)selectManyQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingle() =>
        ObserveIndex(null, false);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingle(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
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
    public IObservableScalarQuery<TElement> ObserveSingleOrDefault() =>
        ObserveIndex(null, true);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSingleOrDefault(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
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
    public IObservableCollectionQuery<TElement> ObserveSkip(int count) =>
        ObserveSlice(new Index(Math.Abs(count), count < 0)..^0);

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveSlice(Range range)
    {
        ObservableCollectionSliceQuery<TElement> sliceQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedSliceQueries ??= []).TryGetValue(range, out sliceQuery!))
            {
                sliceQuery = new ObservableCollectionSliceQuery<TElement>(collectionObserver, this, range);
                cachedSliceQueries.Add(range, sliceQuery);
            }
            ++sliceQuery.Observations;
        }
        sliceQuery.Initialize();
        return sliceQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TElement> ObserveSum() =>
        ObserveSum(IdentityLambdas.Selector);

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TElement, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObservableQuery sumQuery;
        var key = selector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, TResult>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedSumQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out sumQuery!))
            {
                sumQuery = new ObservableCollectionSumQuery<TElement, TResult>(collectionObserver, this, key);
                cachedSumQueries.Add(key, sumQuery);
            }
            ++sumQuery.Observations;
        }
        sumQuery.Initialize();
        return ((ObservableCollectionSumQuery<TElement, TResult>)sumQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveTake(int count) =>
        ObserveSlice(0..new Index(Math.Abs(count), count < 0));

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, IdentityLambdas.Selector, equalityComparer);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector)
        where TKey : notnull =>
        ObserveToDictionary(keySelector, valueSelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(equalityComparer);
        ObservableQuery toDictionaryQuery;
        var key = (keySelector, valueSelector, equalityComparer);
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = ((Expression<Func<TElement, TKey>>)optimizer(keySelector), (Expression<Func<TElement, TValue>>)optimizer(valueSelector), equalityComparer);
        lock (CachedQueriesAccess)
        {
            if (!(cachedToDictionaryQueries ??= new(CachedToDictionaryQueryEqualityComparer.Default)).TryGetValue(key, out toDictionaryQuery!))
            {
                toDictionaryQuery = new ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(collectionObserver, this, key.keySelector, key.valueSelector, key.equalityComparer);
                cachedToDictionaryQueries.Add(key, toDictionaryQuery);
            }
            ++toDictionaryQuery.Observations;
        }
        toDictionaryQuery.Initialize();
        return ((ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>)toDictionaryQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableLookupQuery<TKey, TElement> ObserveToLookup<TKey>(Expression<Func<TElement, TKey>> keySelector)
        where TKey : notnull =>
        ObserveToLookup(keySelector, EqualityComparer<TKey>.Default);

    [return: DisposeWhenDiscarded]
    public IObservableLookupQuery<TKey, TElement> ObserveToLookup<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(keyEqualityComparer);
        ObservableQuery lookupQuery;
        var optimizedKeySelector = keySelector;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            optimizedKeySelector = (Expression<Func<TElement, TKey>>)optimizer(optimizedKeySelector);
        var key = (optimizedKeySelector, keyEqualityComparer);
        lock (CachedQueriesAccess)
        {
            if (!(cachedToLookupQueries ??= new(CachedLookupQueryEqualityComparer.Default)).TryGetValue(key, out lookupQuery!))
            {
                lookupQuery = new ObservableCollectionLookupQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
                cachedToLookupQueries.Add(key, lookupQuery);
            }
            ++lookupQuery.Observations;
        }
        lookupQuery.Initialize();
        return ((ObservableCollectionLookupQuery<TKey, TElement>)lookupQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallback(object context, CollectionSynchronizationCallback synchronizationCallback)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(synchronizationCallback);
        ObservableCollectionUsingSynchronizationCallbackQuery<TElement> usingSynchronizationCallbackQuery;
        var key = (context, synchronizationCallback);
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationCallbackQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackQuery!))
            {
                usingSynchronizationCallbackQuery = new ObservableCollectionUsingSynchronizationCallbackQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                cachedUsingSynchronizationCallbackQueries.Add(key, usingSynchronizationCallbackQuery);
            }
            ++usingSynchronizationCallbackQuery.Observations;
        }
        usingSynchronizationCallbackQuery.Initialize();
        return usingSynchronizationCallbackQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallbackEventually(object context, CollectionSynchronizationCallback synchronizationCallback)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(synchronizationCallback);
        ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> usingSynchronizationCallbackEventuallyQuery;
        var key = (context, synchronizationCallback);
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationCallbackEventuallyQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackEventuallyQuery!))
            {
                usingSynchronizationCallbackEventuallyQuery = new ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                cachedUsingSynchronizationCallbackEventuallyQueries.Add(key, usingSynchronizationCallbackEventuallyQuery);
            }
            ++usingSynchronizationCallbackEventuallyQuery.Observations;
        }
        usingSynchronizationCallbackEventuallyQuery.Initialize();
        return usingSynchronizationCallbackEventuallyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        ObservableCollectionUsingSynchronizationContextQuery<TElement> usingSynchronizationContextQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationContextQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                usingSynchronizationContextQuery = new ObservableCollectionUsingSynchronizationContextQuery<TElement>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextQueries.Add(synchronizationContext, usingSynchronizationContextQuery);
            }
            ++usingSynchronizationContextQuery.Observations;
        }
        usingSynchronizationContextQuery.Initialize();
        return usingSynchronizationContextQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> usingSynchronizationContextEventuallyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSynchronizationContextEventuallyQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                usingSynchronizationContextEventuallyQuery = new ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(collectionObserver, this, synchronizationContext);
                cachedUsingSynchronizationContextEventuallyQueries.Add(synchronizationContext, usingSynchronizationContextEventuallyQuery);
            }
            ++usingSynchronizationContextEventuallyQuery.Observations;
        }
        usingSynchronizationContextEventuallyQuery.Initialize();
        return usingSynchronizationContextEventuallyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSyncRoot(object lockObject)
    {
        ArgumentNullException.ThrowIfNull(lockObject);
        ObservableCollectionUsingSyncRootQuery<TElement> usingSyncRootQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSyncRootQueries ??= []).TryGetValue(lockObject, out usingSyncRootQuery!))
            {
                usingSyncRootQuery = new ObservableCollectionUsingSyncRootQuery<TElement>(collectionObserver, this, lockObject);
                cachedUsingSyncRootQueries.Add(lockObject, usingSyncRootQuery);
            }
            ++usingSyncRootQuery.Observations;
        }
        usingSyncRootQuery.Initialize();
        return usingSyncRootQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveUsingSyncRootEventually(object lockObject)
    {
        ArgumentNullException.ThrowIfNull(lockObject);
        ObservableCollectionUsingSyncRootEventuallyQuery<TElement> usingSyncRootEventuallyQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedUsingSyncRootEventuallyQueries ??= []).TryGetValue(lockObject, out usingSyncRootEventuallyQuery!))
            {
                usingSyncRootEventuallyQuery = new ObservableCollectionUsingSyncRootEventuallyQuery<TElement>(collectionObserver, this, lockObject);
                cachedUsingSyncRootEventuallyQueries.Add(lockObject, usingSyncRootEventuallyQuery);
            }
            ++usingSyncRootEventuallyQuery.Observations;
        }
        usingSyncRootEventuallyQuery.Initialize();
        return usingSyncRootEventuallyQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveWhere(Expression<Func<TElement, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ObservableCollectionWhereQuery<TElement> whereQuery;
        var key = predicate;
        if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
            key = (Expression<Func<TElement, bool>>)optimizer(key);
        lock (CachedQueriesAccess)
        {
            if (!(cachedWhereQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out whereQuery!))
            {
                whereQuery = new ObservableCollectionWhereQuery<TElement>(collectionObserver, this, key);
                cachedWhereQueries.Add(key, whereQuery);
            }
            ++whereQuery.Observations;
        }
        whereQuery.Initialize();
        return whereQuery.AsScoped();
    }

    #endregion Observation Methods

    #region Query Disposal Methods

    internal bool QueryDisposed<TAccumulate, TResult>(ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult> aggregateQuery)
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

    internal bool QueryDisposed(ObservableCollectionAllQuery<TElement> allQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --allQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAllQueries!.Remove(allQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAnyQuery<TElement> anyQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --anyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAnyQueries!.Remove(anyQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAppendQuery<TElement> appendQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --appendQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAppendQueries!.Remove(appendQuery.Appended);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionAverageQuery<TElement, TResult> averageQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --averageQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedAverageQueries!.Remove(averageQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionComparisonQuery<TElement> comparisonQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --comparisonQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedComparisonQueries!.Remove((comparisonQuery.SoughtComparison, comparisonQuery.Comparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionConcatQuery<TElement> concatQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --concatQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedConcatQueries!.Remove(concatQuery.Second);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey>(ObservableCollectionGroupByQuery<TKey, TElement> groupByQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --groupByQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedGroupByQueries!.Remove((groupByQuery.KeySelector, groupByQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionCountQuery<TElement> countQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --countQuery.Observations;
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

    internal bool QueryDisposed(ObservableCollectionIndexForCountQuery<TElement> indexForCountQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --indexForCountQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedIndexForCountQueries!.Remove(indexForCountQuery.IndexForCount);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndexQuery<TElement> indexQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --indexQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedIndexQueries!.Remove((indexQuery.Index, indexQuery.OutOfRangeIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndividualChangesQuery<TElement> individualChangesQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --individualChangesQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedIndividualChangeQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey>(ObservableCollectionLookupQuery<TKey, TElement> lookupQuery)
        where TKey : notnull
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --lookupQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedToLookupQueries!.Remove((lookupQuery.KeySelector, lookupQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionOrderByQuery<TElement> orderByQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --orderByQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedOrderByQueries!.Remove(orderByQuery.SelectorsAndDirections);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionPrependQuery<TElement> prependQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --prependQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedPrependQueries!.Remove(prependQuery.Prepended);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSelectQuery<TElement, TResult> selectQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --selectQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedSelectQueries!.Remove(selectQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSelectManyQuery<TElement, TResult> selectManyQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --selectManyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedSelectManyQueries!.Remove(selectManyQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionSliceQuery<TElement> sliceQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --sliceQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedSliceQueries!.Remove(sliceQuery.Range);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSumQuery<TElement, TResult> sumQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --sumQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedSumQueries!.Remove(sumQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey, TValue>(ObservableCollectionToDictionaryQuery<TElement, TKey, TValue> toDictionaryQuery)
        where TKey : notnull
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --toDictionaryQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedToDictionaryQueries!.Remove((toDictionaryQuery.KeySelector, toDictionaryQuery.ValueSelector, toDictionaryQuery.EqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> usingSynchronizationCallbackEventuallyQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSynchronizationCallbackEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSynchronizationCallbackEventuallyQueries!.Remove((usingSynchronizationCallbackEventuallyQuery.Context, usingSynchronizationCallbackEventuallyQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackQuery<TElement> usingSynchronizationCallbackQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSynchronizationCallbackQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSynchronizationCallbackQueries!.Remove((usingSynchronizationCallbackQuery.Context, usingSynchronizationCallbackQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> usingSynchronizationContextEventuallyQuery)
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

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextQuery<TElement> usingSynchronizationContextQuery)
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

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootEventuallyQuery<TElement> usingSyncRootEventuallyQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSyncRootEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSyncRootEventuallyQueries!.Remove(usingSyncRootEventuallyQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootQuery<TElement> usingSyncRootQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --usingSyncRootQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedUsingSyncRootQueries!.Remove(usingSyncRootQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionWhereQuery<TElement> whereQuery)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --whereQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedWhereQueries!.Remove(whereQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    #endregion Query Disposal Methods
}
