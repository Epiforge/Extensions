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
    /// Holds a collection query's caches of the kinds of query built over it less often than a projection, a flattening, a filter or a concatenation, whose caches the query keeps itself, created with the first of them, so that a query over which none is built carries one field for these rather than twenty-three
    /// </summary>
    sealed class ChildQueryCaches
    {
        internal Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery>? AggregateQueries;
        internal Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionAllQuery<TElement>>? AllQueries;
        internal NullableKeyDictionary<Expression<Func<TElement, bool>>?, ObservableCollectionAnyQuery<TElement>>? AnyQueries;
        internal NullableKeyDictionary<TElement, ObservableCollectionAppendQuery<TElement>>? AppendQueries;
        internal Dictionary<Expression, ObservableQuery>? AverageQueries;
        internal Dictionary<(int soughtComparison, IComparer<TElement> comparer), ObservableCollectionComparisonQuery<TElement>>? ComparisonQueries;
        internal ObservableCollectionCountQuery<TElement>? CountQuery;
        internal Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery>? GroupByQueries;
        internal Dictionary<Func<int, int>, ObservableCollectionIndexForCountQuery<TElement>>? IndexForCountQueries;
        internal Dictionary<(Index? index, bool outOfRangeIsDefault), ObservableQuery>? IndexQueries;
        internal ObservableCollectionIndividualChangesQuery<TElement>? IndividualChangeQuery;
        internal Dictionary<IReadOnlyList<(Expression<Func<TElement, IComparable>> selector, bool isDescending)>, ObservableCollectionOrderByQuery<TElement>>? OrderByQueries;
        internal NullableKeyDictionary<TElement, ObservableCollectionPrependQuery<TElement>>? PrependQueries;
        internal Dictionary<Range, ObservableCollectionSliceQuery<TElement>>? SliceQueries;
        internal Dictionary<Expression, ObservableQuery>? SumQueries;
        internal Dictionary<(Expression keySelector, Expression valueSelector, object equalityComparer), ObservableQuery>? ToDictionaryQueries;
        internal Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery>? ToLookupQueries;
        internal Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>>? UsingSynchronizationCallbackEventuallyQueries;
        internal Dictionary<(object context, CollectionSynchronizationCallback synchronizationCallback), ObservableCollectionUsingSynchronizationCallbackQuery<TElement>>? UsingSynchronizationCallbackQueries;
        internal Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>>? UsingSynchronizationContextEventuallyQueries;
        internal Dictionary<SynchronizationContext, ObservableCollectionUsingSynchronizationContextQuery<TElement>>? UsingSynchronizationContextQueries;
        internal Dictionary<object, ObservableCollectionUsingSyncRootEventuallyQuery<TElement>>? UsingSyncRootEventuallyQueries;
        internal Dictionary<object, ObservableCollectionUsingSyncRootQuery<TElement>>? UsingSyncRootQueries;
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

    Dictionary<IObservableCollectionQuery<TElement>, ObservableCollectionConcatQuery<TElement>>? cachedConcatQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectManyQueries;
    Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionWhereQuery<TElement>>? cachedWhereQueries;
    ChildQueryCaches? childQueryCaches;
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
            if (ExistingChildrenAccess is not { } access)
                return 0;
            var count = 0;
            lock (access)
            {
                count += cachedConcatQueries?.Values.Sum(concatQuery => 1 + concatQuery.CachedObservableQueries) ?? 0;
                count += cachedSelectQueries?.Values.Sum(selectQuery => 1 + selectQuery.CachedObservableQueries) ?? 0;
                count += cachedSelectManyQueries?.Values.Sum(selectManyQuery => 1 + selectManyQuery.CachedObservableQueries) ?? 0;
                count += cachedWhereQueries?.Values.Sum(whereQuery => 1 + whereQuery.CachedObservableQueries) ?? 0;
                if (childQueryCaches is { } caches)
                {
                    count += caches.AggregateQueries?.Values.Sum(aggregateQuery => 1 + aggregateQuery.CachedObservableQueries) ?? 0;
                    count += caches.AllQueries?.Values.Sum(allQuery => 1 + allQuery.CachedObservableQueries) ?? 0;
                    count += caches.AnyQueries?.Values.Sum(anyQuery => 1 + anyQuery.CachedObservableQueries) ?? 0;
                    count += caches.AppendQueries?.Values.Sum(appendQuery => 1 + appendQuery.CachedObservableQueries) ?? 0;
                    count += caches.AverageQueries?.Values.Sum(averageQuery => 1 + averageQuery.CachedObservableQueries) ?? 0;
                    count += caches.ComparisonQueries?.Values.Sum(comparerQuery => 1 + comparerQuery.CachedObservableQueries) ?? 0;
                    count += caches.CountQuery is null ? 0 : 1 + caches.CountQuery.CachedObservableQueries;
                    count += caches.GroupByQueries?.Values.Sum(groupByQuery => 1 + groupByQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndexForCountQueries?.Values.Sum(indexForCountQuery => 1 + indexForCountQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndexQueries?.Values.Sum(indexQuery => 1 + indexQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndividualChangeQuery is null ? 0 : 1 + caches.IndividualChangeQuery.CachedObservableQueries;
                    count += caches.OrderByQueries?.Values.Sum(orderByQuery => 1 + orderByQuery.CachedObservableQueries) ?? 0;
                    count += caches.PrependQueries?.Values.Sum(prependQuery => 1 + prependQuery.CachedObservableQueries) ?? 0;
                    count += caches.SliceQueries?.Values.Sum(sliceQuery => 1 + sliceQuery.CachedObservableQueries) ?? 0;
                    count += caches.SumQueries?.Values.Sum(sumQuery => 1 + sumQuery.CachedObservableQueries) ?? 0;
                    count += caches.ToDictionaryQueries?.Values.Sum(toDictionaryQuery => 1 + toDictionaryQuery.CachedObservableQueries) ?? 0;
                    count += caches.ToLookupQueries?.Values.Sum(toLookupQuery => 1 + toLookupQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSynchronizationCallbackEventuallyQueries?.Values.Sum(usingSynchronizationCallbackEventuallyQuery => 1 + usingSynchronizationCallbackEventuallyQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSynchronizationCallbackQueries?.Values.Sum(usingSynchronizationCallbackQuery => 1 + usingSynchronizationCallbackQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSynchronizationContextEventuallyQueries?.Values.Sum(usingSynchronizationContextEventuallyQuery => 1 + usingSynchronizationContextEventuallyQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSynchronizationContextQueries?.Values.Sum(usingSynchronizationContextQuery => 1 + usingSynchronizationContextQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSyncRootEventuallyQueries?.Values.Sum(usingSyncRootEventuallyQuery => 1 + usingSyncRootEventuallyQuery.CachedObservableQueries) ?? 0;
                    count += caches.UsingSyncRootQueries?.Values.Sum(usingSyncRootQuery => 1 + usingSyncRootQuery.CachedObservableQueries) ?? 0;
                }
            }
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
        CollectionChanged is not null || HasDependents;

    protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!DeferNotification(e))
            RaiseCollectionChanged(e);
    }

    void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        var eventArgs = Logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        Logger?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        NotifyDependents(e);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            var key = (seedFactory, func, resultSelector);
            if (!(caches.AggregateQueries ??= []).TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = new ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
                caches.AggregateQueries.Add(key, aggregateQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AllQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out allQuery!))
            {
                allQuery = new ObservableCollectionAllQuery<TElement>(collectionObserver, this, key);
                caches.AllQueries.Add(key, allQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(null, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this);
                caches.AnyQueries.Add(null, anyQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(key, out anyQuery!))
            {
                anyQuery = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this, key);
                caches.AnyQueries.Add(key, anyQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AppendQueries ??= []).TryGetValue(element, out appendQuery!))
            {
                appendQuery = new ObservableCollectionAppendQuery<TElement>(collectionObserver, this, element);
                caches.AppendQueries.Add(element, appendQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AverageQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out averageQuery!))
            {
                averageQuery = new ObservableCollectionAverageQuery<TElement, TResult>(collectionObserver, this, key);
                caches.AverageQueries.Add(key, averageQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.ComparisonQueries ??= []).TryGetValue((soughtComparison, comparer), out comparisonQuery!))
            {
                comparisonQuery = new ObservableCollectionComparisonQuery<TElement>(collectionObserver, this, soughtComparison, comparer);
                caches.ComparisonQueries.Add((soughtComparison, comparer), comparisonQuery);
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
        lock (ChildrenAccess)
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
        ObservableCollectionCountQuery<TElement> countQuery;
        lock (ChildrenAccess)
        {
            countQuery = (childQueryCaches ??= new()).CountQuery ??= new ObservableCollectionCountQuery<TElement>(collectionObserver, this);
            ++countQuery.Observations;
        }
        countQuery.Initialize();
        return countQuery.AsScoped();
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            caches.IndexForCountQueries ??= [];
            if (!caches.IndexForCountQueries.TryGetValue(indexForCount, out indexForCountQuery!))
            {
                indexForCountQuery = new ObservableCollectionIndexForCountQuery<TElement>(collectionObserver, this, indexForCount);
                caches.IndexForCountQueries.Add(indexForCount, indexForCountQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.IndexQueries ??= []).TryGetValue((index, outOfRangeIsDefault), out var cachedIndexQuery))
            {
                indexQuery = new ObservableCollectionIndexQuery<TElement>(collectionObserver, this, index, outOfRangeIsDefault);
                caches.IndexQueries.Add((index, outOfRangeIsDefault), indexQuery);
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
        ObservableCollectionIndividualChangesQuery<TElement> individualChangesQuery;
        lock (ChildrenAccess)
        {
            individualChangesQuery = (childQueryCaches ??= new()).IndividualChangeQuery ??= new ObservableCollectionIndividualChangesQuery<TElement>(collectionObserver, this);
            ++individualChangesQuery.Observations;
        }
        individualChangesQuery.Initialize();
        return individualChangesQuery.AsScoped();
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.GroupByQueries ??= new(CachedGroupByQueryEqualityComparer.Default)).TryGetValue(key, out groupByQuery!))
            {
                groupByQuery = new ObservableCollectionGroupByQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
                caches.GroupByQueries.Add(key, groupByQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.OrderByQueries ??= new(CachedOrderByQueryEqualityComparer.Default)).TryGetValue(key, out orderByQuery!))
            {
                orderByQuery = new ObservableCollectionOrderByQuery<TElement>(collectionObserver, this, selectorsAndDirections);
                caches.OrderByQueries.Add(key, orderByQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.PrependQueries ??= []).TryGetValue(element, out prependQuery!))
            {
                prependQuery = new ObservableCollectionPrependQuery<TElement>(collectionObserver, this, element);
                caches.PrependQueries.Add(element, prependQuery);
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
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.SliceQueries ??= []).TryGetValue(range, out sliceQuery!))
            {
                sliceQuery = new ObservableCollectionSliceQuery<TElement>(collectionObserver, this, range);
                caches.SliceQueries.Add(range, sliceQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.SumQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out sumQuery!))
            {
                sumQuery = new ObservableCollectionSumQuery<TElement, TResult>(collectionObserver, this, key);
                caches.SumQueries.Add(key, sumQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.ToDictionaryQueries ??= new(CachedToDictionaryQueryEqualityComparer.Default)).TryGetValue(key, out toDictionaryQuery!))
            {
                toDictionaryQuery = new ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(collectionObserver, this, key.keySelector, key.valueSelector, key.equalityComparer);
                caches.ToDictionaryQueries.Add(key, toDictionaryQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.ToLookupQueries ??= new(CachedLookupQueryEqualityComparer.Default)).TryGetValue(key, out lookupQuery!))
            {
                lookupQuery = new ObservableCollectionLookupQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
                caches.ToLookupQueries.Add(key, lookupQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSynchronizationCallbackQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackQuery!))
            {
                usingSynchronizationCallbackQuery = new ObservableCollectionUsingSynchronizationCallbackQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                caches.UsingSynchronizationCallbackQueries.Add(key, usingSynchronizationCallbackQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSynchronizationCallbackEventuallyQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackEventuallyQuery!))
            {
                usingSynchronizationCallbackEventuallyQuery = new ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
                caches.UsingSynchronizationCallbackEventuallyQueries.Add(key, usingSynchronizationCallbackEventuallyQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSynchronizationContextQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                usingSynchronizationContextQuery = new ObservableCollectionUsingSynchronizationContextQuery<TElement>(collectionObserver, this, synchronizationContext);
                caches.UsingSynchronizationContextQueries.Add(synchronizationContext, usingSynchronizationContextQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSynchronizationContextEventuallyQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                usingSynchronizationContextEventuallyQuery = new ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(collectionObserver, this, synchronizationContext);
                caches.UsingSynchronizationContextEventuallyQueries.Add(synchronizationContext, usingSynchronizationContextEventuallyQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSyncRootQueries ??= []).TryGetValue(lockObject, out usingSyncRootQuery!))
            {
                usingSyncRootQuery = new ObservableCollectionUsingSyncRootQuery<TElement>(collectionObserver, this, lockObject);
                caches.UsingSyncRootQueries.Add(lockObject, usingSyncRootQuery);
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
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.UsingSyncRootEventuallyQueries ??= []).TryGetValue(lockObject, out usingSyncRootEventuallyQuery!))
            {
                usingSyncRootEventuallyQuery = new ObservableCollectionUsingSyncRootEventuallyQuery<TElement>(collectionObserver, this, lockObject);
                caches.UsingSyncRootEventuallyQueries.Add(lockObject, usingSyncRootEventuallyQuery);
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
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
        {
            var remaining = --aggregateQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.AggregateQueries!.Remove((aggregateQuery.SeedFactory, aggregateQuery.Func, aggregateQuery.ResultSelector));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAllQuery<TElement> allQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --allQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.AllQueries!.Remove(allQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAnyQuery<TElement> anyQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --anyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.AnyQueries!.Remove(anyQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionAppendQuery<TElement> appendQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --appendQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.AppendQueries!.Remove(appendQuery.Appended);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionAverageQuery<TElement, TResult> averageQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --averageQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.AverageQueries!.Remove(averageQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionComparisonQuery<TElement> comparisonQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --comparisonQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.ComparisonQueries!.Remove((comparisonQuery.SoughtComparison, comparisonQuery.Comparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionConcatQuery<TElement> concatQuery)
    {
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
        {
            var remaining = --groupByQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.GroupByQueries!.Remove((groupByQuery.KeySelector, groupByQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionCountQuery<TElement> countQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --countQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.CountQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndexForCountQuery<TElement> indexForCountQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --indexForCountQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.IndexForCountQueries!.Remove(indexForCountQuery.IndexForCount);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndexQuery<TElement> indexQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --indexQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.IndexQueries!.Remove((indexQuery.Index, indexQuery.OutOfRangeIsDefault));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionIndividualChangesQuery<TElement> individualChangesQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --individualChangesQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.IndividualChangeQuery = null;
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey>(ObservableCollectionLookupQuery<TKey, TElement> lookupQuery)
        where TKey : notnull
    {
        lock (ChildrenAccess)
        {
            var remaining = --lookupQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.ToLookupQueries!.Remove((lookupQuery.KeySelector, lookupQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionOrderByQuery<TElement> orderByQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --orderByQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.OrderByQueries!.Remove(orderByQuery.SelectorsAndDirections);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionPrependQuery<TElement> prependQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --prependQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.PrependQueries!.Remove(prependQuery.Prepended);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSelectQuery<TElement, TResult> selectQuery)
    {
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
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
        lock (ChildrenAccess)
        {
            var remaining = --sliceQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.SliceQueries!.Remove(sliceQuery.Range);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionSumQuery<TElement, TResult> sumQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --sumQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.SumQueries!.Remove(sumQuery.Selector);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey, TValue>(ObservableCollectionToDictionaryQuery<TElement, TKey, TValue> toDictionaryQuery)
        where TKey : notnull
    {
        lock (ChildrenAccess)
        {
            var remaining = --toDictionaryQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.ToDictionaryQueries!.Remove((toDictionaryQuery.KeySelector, toDictionaryQuery.ValueSelector, toDictionaryQuery.EqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> usingSynchronizationCallbackEventuallyQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSynchronizationCallbackEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSynchronizationCallbackEventuallyQueries!.Remove((usingSynchronizationCallbackEventuallyQuery.Context, usingSynchronizationCallbackEventuallyQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationCallbackQuery<TElement> usingSynchronizationCallbackQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSynchronizationCallbackQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSynchronizationCallbackQueries!.Remove((usingSynchronizationCallbackQuery.Context, usingSynchronizationCallbackQuery.SynchronizationCallback));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> usingSynchronizationContextEventuallyQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSynchronizationContextEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSynchronizationContextEventuallyQueries!.Remove(usingSynchronizationContextEventuallyQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSynchronizationContextQuery<TElement> usingSynchronizationContextQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSynchronizationContextQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSynchronizationContextQueries!.Remove(usingSynchronizationContextQuery.SynchronizationContext);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootEventuallyQuery<TElement> usingSyncRootEventuallyQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSyncRootEventuallyQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSyncRootEventuallyQueries!.Remove(usingSyncRootEventuallyQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionUsingSyncRootQuery<TElement> usingSyncRootQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --usingSyncRootQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                childQueryCaches!.UsingSyncRootQueries!.Remove(usingSyncRootQuery.SyncRoot);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionWhereQuery<TElement> whereQuery)
    {
        lock (ChildrenAccess)
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
