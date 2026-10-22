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
    /// Holds a collection query's caches of the kinds of query built over it less often than a projection, a flattening, a filter or a concatenation, whose caches the query keeps itself, created with the first of them, so that a query over which none is built carries one field for these rather than twenty-five
    /// </summary>
    sealed class ChildQueryCaches
    {
        internal Dictionary<(object seedFactory, object func, object resultSelector), ObservableQuery>? AggregateQueries;
        internal Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionAllQuery<TElement>>? AllQueries;
        internal NullableKeyDictionary<Expression<Func<TElement, bool>>?, ObservableCollectionAnyQuery<TElement>>? AnyQueries;
        internal NullableKeyDictionary<TElement, ObservableCollectionAppendQuery<TElement>>? AppendQueries;
        internal Dictionary<Expression, ObservableQuery>? AverageQueries;
        internal Dictionary<Type, ObservableQuery>? CastQueries;
        internal Dictionary<(int soughtComparison, IComparer<TElement> comparer), ObservableCollectionComparisonQuery<TElement>>? ComparisonQueries;
        internal ObservableCollectionCountQuery<TElement>? CountQuery;
        internal Dictionary<(Expression keySelector, object keyEqualityComparer), ObservableQuery>? GroupByQueries;
        internal Dictionary<Func<int, int>, ObservableCollectionIndexForCountQuery<TElement>>? IndexForCountQueries;
        internal Dictionary<(Index? index, bool outOfRangeIsDefault), ObservableQuery>? IndexQueries;
        internal ObservableCollectionIndividualChangesQuery<TElement>? IndividualChangeQuery;
        internal Dictionary<Type, ObservableQuery>? OfTypeQueries;
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

    /// <summary>
    /// Stands among the deferred notifications for an announcement of the count, made for a change of which nothing is told in any other way
    /// </summary>
    static readonly object countAnnouncement = new();
    /// <summary>
    /// The name a collection gives its indexer when it announces that the elements it holds have changed
    /// </summary>
    const string indexerPropertyName = "Item[]";
    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    /// <summary>
    /// The count this query last announced, from which it tells whether a change it announces changes the count, kept as -2 less it while the count is announced as changing, so that <see cref="Count"/> then reads it and a subscriber finds the count the change is leaving; -1 until the query is initialized, and for a query which does not announce its count
    /// </summary>
    int announcedCount = -1;
    Dictionary<IObservableCollectionQuery<TElement>, ObservableCollectionConcatQuery<TElement>>? cachedConcatQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectQueries;
    Dictionary<Expression, ObservableQuery>? cachedSelectManyQueries;
    Dictionary<Expression<Func<TElement, bool>>, ObservableCollectionWhereQuery<TElement>>? cachedWhereQueries;
    ChildQueryCaches? childQueryCaches;
    Exception? inheritedOperationFault;
    Exception? operationFault;
    Exception? ownOperationFault;

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
                    count += caches.CastQueries?.Values.Sum(castQuery => 1 + castQuery.CachedObservableQueries) ?? 0;
                    count += caches.ComparisonQueries?.Values.Sum(comparerQuery => 1 + comparerQuery.CachedObservableQueries) ?? 0;
                    count += caches.CountQuery is null ? 0 : 1 + caches.CountQuery.CachedObservableQueries;
                    count += caches.GroupByQueries?.Values.Sum(groupByQuery => 1 + groupByQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndexForCountQueries?.Values.Sum(indexForCountQuery => 1 + indexForCountQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndexQueries?.Values.Sum(indexQuery => 1 + indexQuery.CachedObservableQueries) ?? 0;
                    count += caches.IndividualChangeQuery is null ? 0 : 1 + caches.IndividualChangeQuery.CachedObservableQueries;
                    count += caches.OfTypeQueries?.Values.Sum(ofTypeQuery => 1 + ofTypeQuery.CachedObservableQueries) ?? 0;
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

    public int Count =>
        Volatile.Read(ref announcedCount) is < -1 and var changing ? -2 - changing : CurrentCount;

    int ICollection.Count =>
        Count;

    int IReadOnlyCollection<TElement>.Count =>
        Count;

    /// <summary>
    /// Gets the number of elements this query holds now
    /// </summary>
    private protected abstract int CurrentCount { get; }

    internal virtual bool HasEnumerationPenalty =>
        false;

    internal virtual bool HasIndexerPenalty =>
        false;

    public virtual bool IsSynchronized =>
        false;

    public virtual Exception? OperationFault
    {
        get => operationFault;
        protected set
        {
            if (FaultList.DescribeTheSame(ownOperationFault, value))
                return;
            ownOperationFault = value;
            PublishOperationFault();
        }
    }

    /// <summary>
    /// The fault of this query's own evaluations, without the fault of the query it is built over which <see cref="OperationFault" /> also reports
    /// </summary>
    private protected Exception? OwnOperationFault =>
        ownOperationFault;

    /// <summary>
    /// Takes on the fault of the query this one is built over, so that a view reports a fault arising anywhere beneath it
    /// </summary>
    private protected void InheritOperationFault(Exception? fault)
    {
        if (ReferenceEquals(inheritedOperationFault, fault))
            return;
        inheritedOperationFault = fault;
        PublishOperationFault();
    }

    void PublishOperationFault()
    {
        Exception? fault;
        if (inheritedOperationFault is null)
            fault = ownOperationFault;
        else if (ownOperationFault is null)
            fault = inheritedOperationFault;
        else
        {
            var faultList = new FaultList(inheritedOperationFault);
            faultList.Add(ownOperationFault);
            fault = faultList.Fault;
        }
        if (FaultList.DescribeTheSame(operationFault, fault))
            return;
        if (SetBackedProperty(ref operationFault, in fault, operationFaultPropertyChangingEventArgs, operationFaultPropertyChangedEventArgs))
            OnOperationFaultPublished(fault);
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
        if (array is TElement[] typedArray && IsCopiedDirectlyInto(typedArray, index) && TryCopyTo(typedArray, index))
            return;
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

    /// <summary>
    /// Determines whether elements may be stored into an array without <see cref="Array.SetValue(object, int)"/>, which is so where the array is exactly an array of the element type, since a store into an array of a derived type would fail differently than <see cref="Array.SetValue(object, int)"/> does
    /// </summary>
    static bool IsCopiedDirectlyInto(TElement[] array, int arrayIndex) =>
        array.GetType() == typeof(TElement[]) && arrayIndex >= 0;

    /// <summary>
    /// Copies the elements into an array of exactly their type, where they fit from the index, without boxing them or checking the type of each store
    /// </summary>
    /// <returns><c>true</c> if the elements were copied; otherwise, <c>false</c>, having written nothing, so that the caller fails as <see cref="Array.SetValue(object, int)"/> would</returns>
    private protected virtual bool TryCopyTo(TElement[] array, int arrayIndex)
    {
        var count = Count;
        if (array.Length - arrayIndex < count)
            return false;
        if (!HasIndexerPenalty)
            for (var i = 0; i < count; ++i)
                array[arrayIndex + i] = this[i];
        else
        {
            var i = arrayIndex - 1;
            foreach (var item in this)
                array[++i] = item;
        }
        return true;
    }

    /// <summary>
    /// Brings this query's copy of an observed sequence up to date with a change the sequence announced and then announces it, reading the whole sequence again where the announcement does not say precisely what changed
    /// </summary>
    private protected void FollowChange(ref List<TElement>? copy, NotifyCollectionChangedEventArgs e, IEnumerable sequence)
    {
        var kept = copy!;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { } newItems && e.NewStartingIndex >= 0 && e.NewStartingIndex <= kept.Count:
                ListEdits.InsertAll(kept, e.NewStartingIndex, newItems);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is { } oldItems && e.OldStartingIndex >= 0 && e.OldStartingIndex + oldItems.Count <= kept.Count:
                kept.RemoveRange(e.OldStartingIndex, oldItems.Count);
                break;
            case NotifyCollectionChangedAction.Replace when e.OldItems is { } replacedItems && e.NewItems is { } replacementItems && e.OldStartingIndex >= 0 && e.OldStartingIndex + replacedItems.Count <= kept.Count:
                if (replacedItems.Count == replacementItems.Count)
                    for (var i = 0; i < replacementItems.Count; ++i)
                        kept[e.OldStartingIndex + i] = (TElement)replacementItems[i]!;
                else
                {
                    kept.RemoveRange(e.OldStartingIndex, replacedItems.Count);
                    ListEdits.InsertAll(kept, e.OldStartingIndex, replacementItems);
                }
                break;
            case NotifyCollectionChangedAction.Move when e.OldItems is { } movedItems && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex + movedItems.Count <= kept.Count && e.NewStartingIndex + movedItems.Count <= kept.Count:
                ListEdits.MoveRange(kept, e.OldStartingIndex, e.NewStartingIndex, movedItems.Count);
                break;
            default:
                copy = sequence is IEnumerable<TElement> typedSequence ? new List<TElement>(typedSequence) : [.. sequence.Cast<TElement>()];
                break;
        }
        OnCollectionChanged(e);
    }

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

    /// <summary>
    /// Gets whether this query announces its count, which it does from the collection notifications it delivers, and so only where it delivers them for every change
    /// </summary>
    private protected virtual bool AnnouncesCount =>
        true;

    private protected bool IsChangeObserved =>
        CollectionChanged is not null || HasDependents;

    /// <summary>
    /// Announces the count a change leaves where no notification describing the change will be delivered, because nothing is listening for one, so that a subscriber listening only for the count is still told
    /// </summary>
    private protected void AnnounceCountOfUnobservedChange()
    {
        if (!IsChangeObserved && !DeferNotification(countAnnouncement))
            AnnounceCount();
    }

    protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!DeferNotification(e))
            RaiseCollectionChanged(e);
    }

    /// <summary>
    /// Called once this query has announced a change in <see cref="OperationFault" />, so that queries which are part of this one can report it
    /// </summary>
    private protected virtual void OnOperationFaultPublished(Exception? fault)
    {
    }

    private protected override void OnInitialized()
    {
        if (AnnouncesCount)
            announcedCount = CurrentCount;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Count) || announcedCount == -1)
            base.OnPropertyChanged(e);
    }

    private protected override void OnRaisingPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == indexerPropertyName)
            AnnounceCount();
    }

    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(Count) || announcedCount == -1)
            base.OnPropertyChanging(e);
    }

    /// <summary>
    /// Announces the count a change leaves, before the first of the notifications describing it, as a collection which changes in place does, so that the count is announced as changing while it still reads what it was and as changed once it reads what the change leaves
    /// </summary>
    /// <remarks>
    /// The count announced is the one this query holds, which is the one the change leaves because a query delivers each notification while it is as that notification leaves it
    /// </remarks>
    void AnnounceCount()
    {
        var announced = announcedCount;
        if (announced < 0)
            return;
        var count = CurrentCount;
        if (count == announced)
            return;
        Volatile.Write(ref announcedCount, -2 - announced);
        try
        {
            RaisePropertyChanging(countPropertyChangingEventArgs);
        }
        finally
        {
            Volatile.Write(ref announcedCount, count);
        }
        RaisePropertyChanged(countPropertyChangedEventArgs);
    }

    void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        AnnounceCount();
        var eventArgs = Logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        Logger.WhenTracing()?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        NotifyDependents(e);
        CollectionChanged?.Invoke(this, e);
        Logger.WhenTracing()?.LogTrace(Collections.EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    private protected override void RaiseNotification(object eventArguments)
    {
        if (ReferenceEquals(eventArguments, countAnnouncement))
            AnnounceCount();
        else if (eventArguments is NotifyCollectionChangedEventArgs collectionChangedEventArgs)
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
            if ((caches.AggregateQueries ??= []).TryGetValue(key, out var cachedAggregateQuery))
            {
                aggregateQuery = (ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>)cachedAggregateQuery;
                ++aggregateQuery.Observations;
                return aggregateQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>(collectionObserver, this, seedFactory, func, resultSelector);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            var key = (seedFactory, func, resultSelector);
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.AggregateQueries ??= [], key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            aggregateQuery = (ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>)cached!;
            ++aggregateQuery.Observations;
        }
        built.Release();
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
            if ((caches.AllQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out allQuery!))
            {
                ++allQuery.Observations;
                return allQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionAllQuery<TElement>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.AllQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            allQuery = cached!;
            ++allQuery.Observations;
        }
        built.Release();
        return allQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<bool> ObserveAny()
    {
        ObservableCollectionAnyQuery<TElement> anyQuery;
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if ((caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(null, out anyQuery!))
            {
                ++anyQuery.Observations;
                return anyQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(null, out anyQuery!))
            {
                caches.AnyQueries.Add(null, built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++anyQuery.Observations;
        }
        built.Release();
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
            if ((caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(key, out anyQuery!))
            {
                ++anyQuery.Observations;
                return anyQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionAnyQuery<TElement>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AnyQueries ??= new(ExpressionEqualityComparer.Default!)).TryGetValue(key, out anyQuery!))
            {
                caches.AnyQueries.Add(key, built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++anyQuery.Observations;
        }
        built.Release();
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
            if ((caches.AppendQueries ??= []).TryGetValue(element, out appendQuery!))
            {
                ++appendQuery.Observations;
                return appendQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionAppendQuery<TElement>(collectionObserver, this, element);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.AppendQueries ??= []).TryGetValue(element, out appendQuery!))
            {
                caches.AppendQueries.Add(element, built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++appendQuery.Observations;
        }
        built.Release();
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
            if ((caches.AverageQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out averageQuery!))
            {
                ++averageQuery.Observations;
                return ((ObservableCollectionAverageQuery<TElement, TResult>)averageQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionAverageQuery<TElement, TResult>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.AverageQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionAverageQuery<TElement, TResult>)built).AsScoped();
            }
            averageQuery = cached!;
            ++averageQuery.Observations;
        }
        built.Release();
        return ((ObservableCollectionAverageQuery<TElement, TResult>)averageQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TResult> ObserveCast<TResult>()
    {
        ObservableQuery castQuery;
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if ((caches.CastQueries ??= []).TryGetValue(typeof(TResult), out castQuery!))
            {
                ++castQuery.Observations;
                return ((ObservableCollectionCastQuery<TElement, TResult>)castQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionCastQuery<TElement, TResult>(collectionObserver, this);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.CastQueries ??= []).TryGetValue(typeof(TResult), out castQuery!))
            {
                caches.CastQueries.Add(typeof(TResult), built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++castQuery.Observations;
        }
        built.Release();
        return ((ObservableCollectionCastQuery<TElement, TResult>)castQuery).AsScoped();
    }

    [return: DisposeWhenDiscarded]
    internal IObservableScalarQuery<TElement> ObserveComparison(int soughtComparison, IComparer<TElement> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ObservableCollectionComparisonQuery<TElement> comparisonQuery;
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if ((caches.ComparisonQueries ??= []).TryGetValue((soughtComparison, comparer), out comparisonQuery!))
            {
                ++comparisonQuery.Observations;
                return comparisonQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionComparisonQuery<TElement>(collectionObserver, this, soughtComparison, comparer);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.ComparisonQueries ??= [], (soughtComparison, comparer), out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            comparisonQuery = cached!;
            ++comparisonQuery.Observations;
        }
        built.Release();
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
            if ((cachedConcatQueries ??= []).TryGetValue(second, out concatQuery!))
            {
                ++concatQuery.Observations;
                return concatQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionConcatQuery<TElement>(collectionObserver, this, second);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedConcatQueries ??= [], second, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            concatQuery = cached!;
            ++concatQuery.Observations;
        }
        built.Release();
        return concatQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableScalarQuery<int> ObserveCount()
    {
        ObservableCollectionCountQuery<TElement>? countQuery;
        lock (ChildrenAccess)
        {
            countQuery = childQueryCaches?.CountQuery;
            if (countQuery is not null)
            {
                ++countQuery.Observations;
                return countQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionCountQuery<TElement>(collectionObserver, this);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            countQuery = caches.CountQuery;
            if (countQuery is null)
            {
                caches.CountQuery = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            ++countQuery.Observations;
        }
        built.Release();
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
            if (caches.IndexForCountQueries.TryGetValue(indexForCount, out indexForCountQuery!))
            {
                ++indexForCountQuery.Observations;
                return indexForCountQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionIndexForCountQuery<TElement>(collectionObserver, this, indexForCount);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            caches.IndexForCountQueries ??= [];
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.IndexForCountQueries, indexForCount, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            indexForCountQuery = cached!;
            ++indexForCountQuery.Observations;
        }
        built.Release();
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
            if ((caches.IndexQueries ??= []).TryGetValue((index, outOfRangeIsDefault), out var cachedIndexQuery))
            {
                indexQuery = (ObservableCollectionIndexQuery<TElement>)cachedIndexQuery;
                ++indexQuery.Observations;
                return indexQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionIndexQuery<TElement>(collectionObserver, this, index, outOfRangeIsDefault);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.IndexQueries ??= [], (index, outOfRangeIsDefault), out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            indexQuery = (ObservableCollectionIndexQuery<TElement>)cached!;
            ++indexQuery.Observations;
        }
        built.Release();
        return indexQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObserveIndividualChanges()
    {
        ObservableCollectionIndividualChangesQuery<TElement>? individualChangesQuery;
        lock (ChildrenAccess)
        {
            individualChangesQuery = childQueryCaches?.IndividualChangeQuery;
            if (individualChangesQuery is not null)
            {
                ++individualChangesQuery.Observations;
                return individualChangesQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionIndividualChangesQuery<TElement>(collectionObserver, this);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            individualChangesQuery = caches.IndividualChangeQuery;
            if (individualChangesQuery is null)
            {
                caches.IndividualChangeQuery = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            ++individualChangesQuery.Observations;
        }
        built.Release();
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
            if ((caches.GroupByQueries ??= new(CachedGroupByQueryEqualityComparer.Default)).TryGetValue(key, out groupByQuery!))
            {
                ++groupByQuery.Observations;
                return ((ObservableCollectionGroupByQuery<TKey, TElement>)groupByQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionGroupByQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.GroupByQueries ??= new(CachedGroupByQueryEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionGroupByQuery<TKey, TElement>)built).AsScoped();
            }
            groupByQuery = cached!;
            ++groupByQuery.Observations;
        }
        built.Release();
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
        ObservableQuery ofTypeQuery;
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if ((caches.OfTypeQueries ??= []).TryGetValue(typeof(TResult), out ofTypeQuery!))
            {
                ++ofTypeQuery.Observations;
                return ((ObservableCollectionOfTypeQuery<TElement, TResult>)ofTypeQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionOfTypeQuery<TElement, TResult>(collectionObserver, this);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.OfTypeQueries ??= []).TryGetValue(typeof(TResult), out ofTypeQuery!))
            {
                caches.OfTypeQueries.Add(typeof(TResult), built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++ofTypeQuery.Observations;
        }
        built.Release();
        return ((ObservableCollectionOfTypeQuery<TElement, TResult>)ofTypeQuery).AsScoped();
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
            if ((caches.OrderByQueries ??= new(CachedOrderByQueryEqualityComparer.Default)).TryGetValue(key, out orderByQuery!))
            {
                ++orderByQuery.Observations;
                return orderByQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionOrderByQuery<TElement>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.OrderByQueries ??= new(CachedOrderByQueryEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            orderByQuery = cached!;
            ++orderByQuery.Observations;
        }
        built.Release();
        return orderByQuery.AsScoped();
    }

    [return: DisposeWhenDiscarded]
    public IObservableCollectionQuery<TElement> ObservePrepend(TElement element)
    {
        ObservableCollectionPrependQuery<TElement> prependQuery;
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if ((caches.PrependQueries ??= []).TryGetValue(element, out prependQuery!))
            {
                ++prependQuery.Observations;
                return prependQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionPrependQuery<TElement>(collectionObserver, this, element);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            if (!(caches.PrependQueries ??= []).TryGetValue(element, out prependQuery!))
            {
                caches.PrependQueries.Add(element, built);
                built.IsCached = true;
                return built.AsScoped();
            }
            ++prependQuery.Observations;
        }
        built.Release();
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
            if ((cachedSelectQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out selectQuery!))
            {
                ++selectQuery.Observations;
                return ((ObservableCollectionSelectQuery<TElement, TResult>)selectQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionSelectQuery<TElement, TResult>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedSelectQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionSelectQuery<TElement, TResult>)built).AsScoped();
            }
            selectQuery = cached!;
            ++selectQuery.Observations;
        }
        built.Release();
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
            if ((cachedSelectManyQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out selectManyQuery!))
            {
                ++selectManyQuery.Observations;
                return ((ObservableCollectionSelectManyQuery<TElement, TResult>)selectManyQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionSelectManyQuery<TElement, TResult>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedSelectManyQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionSelectManyQuery<TElement, TResult>)built).AsScoped();
            }
            selectManyQuery = cached!;
            ++selectManyQuery.Observations;
        }
        built.Release();
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
            if ((caches.SliceQueries ??= []).TryGetValue(range, out sliceQuery!))
            {
                ++sliceQuery.Observations;
                return sliceQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionSliceQuery<TElement>(collectionObserver, this, range);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.SliceQueries ??= [], range, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            sliceQuery = cached!;
            ++sliceQuery.Observations;
        }
        built.Release();
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
            if ((caches.SumQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out sumQuery!))
            {
                ++sumQuery.Observations;
                return ((ObservableCollectionSumQuery<TElement, TResult>)sumQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionSumQuery<TElement, TResult>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.SumQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionSumQuery<TElement, TResult>)built).AsScoped();
            }
            sumQuery = cached!;
            ++sumQuery.Observations;
        }
        built.Release();
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
            if ((caches.ToDictionaryQueries ??= new(CachedToDictionaryQueryEqualityComparer.Default)).TryGetValue(key, out toDictionaryQuery!))
            {
                ++toDictionaryQuery.Observations;
                return ((ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>)toDictionaryQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(collectionObserver, this, key.keySelector, key.valueSelector, key.equalityComparer);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.ToDictionaryQueries ??= new(CachedToDictionaryQueryEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>)built).AsScoped();
            }
            toDictionaryQuery = cached!;
            ++toDictionaryQuery.Observations;
        }
        built.Release();
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
            if ((caches.ToLookupQueries ??= new(CachedLookupQueryEqualityComparer.Default)).TryGetValue(key, out lookupQuery!))
            {
                ++lookupQuery.Observations;
                return ((ObservableCollectionLookupQuery<TKey, TElement>)lookupQuery).AsScoped();
            }
        }
        var built = new ObservableCollectionLookupQuery<TKey, TElement>(collectionObserver, this, optimizedKeySelector, keyEqualityComparer);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.ToLookupQueries ??= new(CachedLookupQueryEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableCollectionLookupQuery<TKey, TElement>)built).AsScoped();
            }
            lookupQuery = cached!;
            ++lookupQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSynchronizationCallbackQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackQuery!))
            {
                ++usingSynchronizationCallbackQuery.Observations;
                return usingSynchronizationCallbackQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSynchronizationCallbackQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSynchronizationCallbackQueries ??= [], key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSynchronizationCallbackQuery = cached!;
            ++usingSynchronizationCallbackQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSynchronizationCallbackEventuallyQueries ??= []).TryGetValue(key, out usingSynchronizationCallbackEventuallyQuery!))
            {
                ++usingSynchronizationCallbackEventuallyQuery.Observations;
                return usingSynchronizationCallbackEventuallyQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement>(collectionObserver, this, context, synchronizationCallback);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSynchronizationCallbackEventuallyQueries ??= [], key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSynchronizationCallbackEventuallyQuery = cached!;
            ++usingSynchronizationCallbackEventuallyQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSynchronizationContextQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextQuery!))
            {
                ++usingSynchronizationContextQuery.Observations;
                return usingSynchronizationContextQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSynchronizationContextQuery<TElement>(collectionObserver, this, synchronizationContext);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSynchronizationContextQueries ??= [], synchronizationContext, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSynchronizationContextQuery = cached!;
            ++usingSynchronizationContextQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSynchronizationContextEventuallyQueries ??= []).TryGetValue(synchronizationContext, out usingSynchronizationContextEventuallyQuery!))
            {
                ++usingSynchronizationContextEventuallyQuery.Observations;
                return usingSynchronizationContextEventuallyQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(collectionObserver, this, synchronizationContext);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSynchronizationContextEventuallyQueries ??= [], synchronizationContext, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSynchronizationContextEventuallyQuery = cached!;
            ++usingSynchronizationContextEventuallyQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSyncRootQueries ??= []).TryGetValue(lockObject, out usingSyncRootQuery!))
            {
                ++usingSyncRootQuery.Observations;
                return usingSyncRootQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSyncRootQuery<TElement>(collectionObserver, this, lockObject);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSyncRootQueries ??= [], lockObject, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSyncRootQuery = cached!;
            ++usingSyncRootQuery.Observations;
        }
        built.Release();
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
            if ((caches.UsingSyncRootEventuallyQueries ??= []).TryGetValue(lockObject, out usingSyncRootEventuallyQuery!))
            {
                ++usingSyncRootEventuallyQuery.Observations;
                return usingSyncRootEventuallyQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionUsingSyncRootEventuallyQuery<TElement>(collectionObserver, this, lockObject);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            var caches = childQueryCaches ??= new();
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(caches.UsingSyncRootEventuallyQueries ??= [], lockObject, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            usingSyncRootEventuallyQuery = cached!;
            ++usingSyncRootEventuallyQuery.Observations;
        }
        built.Release();
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
            if ((cachedWhereQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(key, out whereQuery!))
            {
                ++whereQuery.Observations;
                return whereQuery.AsScoped();
            }
        }
        var built = new ObservableCollectionWhereQuery<TElement>(collectionObserver, this, key);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedWhereQueries ??= new(ExpressionEqualityComparer.Default), key, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            whereQuery = cached!;
            ++whereQuery.Observations;
        }
        built.Release();
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
                if (aggregateQuery.IsCached)
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
                if (allQuery.IsCached)
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
                if (anyQuery.IsCached)
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
                if (appendQuery.IsCached)
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
                if (averageQuery.IsCached)
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
                if (comparisonQuery.IsCached)
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
                if (concatQuery.IsCached)
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
                if (groupByQuery.IsCached)
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
                if (countQuery.IsCached)
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
                if (indexForCountQuery.IsCached)
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
                if (indexQuery.IsCached)
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
                if (individualChangesQuery.IsCached)
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
                if (lookupQuery.IsCached)
                    childQueryCaches!.ToLookupQueries!.Remove((lookupQuery.KeySelector, lookupQuery.KeyEqualityComparer));
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionOfTypeQuery<TElement, TResult> ofTypeQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --ofTypeQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (ofTypeQuery.IsCached)
                    childQueryCaches!.OfTypeQueries!.Remove(typeof(TResult));
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
                if (orderByQuery.IsCached)
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
                if (prependQuery.IsCached)
                    childQueryCaches!.PrependQueries!.Remove(prependQuery.Prepended);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TResult>(ObservableCollectionCastQuery<TElement, TResult> castQuery)
    {
        lock (ChildrenAccess)
        {
            var remaining = --castQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (castQuery.IsCached)
                    childQueryCaches!.CastQueries!.Remove(typeof(TResult));
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
                if (selectQuery.IsCached)
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
                if (selectManyQuery.IsCached)
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
                if (sliceQuery.IsCached)
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
                if (sumQuery.IsCached)
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
                if (toDictionaryQuery.IsCached)
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
                if (usingSynchronizationCallbackEventuallyQuery.IsCached)
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
                if (usingSynchronizationCallbackQuery.IsCached)
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
                if (usingSynchronizationContextEventuallyQuery.IsCached)
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
                if (usingSynchronizationContextQuery.IsCached)
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
                if (usingSyncRootEventuallyQuery.IsCached)
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
                if (usingSyncRootQuery.IsCached)
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
                if (whereQuery.IsCached)
                    cachedWhereQueries!.Remove(whereQuery.Predicate);
                return true;
            }
        }
        return false;
    }

    #endregion Query Disposal Methods
}
