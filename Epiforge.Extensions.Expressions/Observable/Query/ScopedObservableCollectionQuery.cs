namespace Epiforge.Extensions.Expressions.Observable.Query;

// Queries are cached and reference counted, so what a caller receives has to be scoped to that
// caller rather than the shared query itself; otherwise one caller's Dispose releases another's
// claim. Every member here forwards to that shared query; only disposal and the events are its own.
// Same pattern as ScopedObservableExpression and ScopedObservableScalarQuery.
class ScopedObservableCollectionQuery<TElement> :
    IObservableCollectionQuery<TElement>
{
    internal ScopedObservableCollectionQuery(ObservableCollectionQuery<TElement> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        this.query = query;
        this.query.PropertyChanged += QueryPropertyChanged;
        this.query.PropertyChanging += QueryPropertyChanging;
        this.query.CollectionChanged += QueryCollectionChanged;
    }

    internal readonly ObservableCollectionQuery<TElement> query;
    int disposed;

    public int CachedObservableQueries =>
        query.CachedObservableQueries;

    public ICollectionObserver CollectionObserver =>
        query.CollectionObserver;

    public bool IsDisposed =>
        disposed != 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event EventHandler<DisposalNotificationEventArgs>? Disposed;

    public event EventHandler<DisposalNotificationEventArgs>? Disposing;

#pragma warning disable CS0067 // disposal here is never overridden: releasing this scope's single claim on the query always succeeds
    public event EventHandler<DisposalNotificationEventArgs>? DisposalOverridden;
#pragma warning restore CS0067

    public virtual void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = DisposalNotificationEventArgs.ByCallingDispose;
        Disposing?.Invoke(this, e);
        query.PropertyChanged -= QueryPropertyChanged;
        query.PropertyChanging -= QueryPropertyChanging;
        query.CollectionChanged -= QueryCollectionChanged;
        query.Dispose();
        Disposed?.Invoke(this, e);
    }

    void QueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        PropertyChanging?.Invoke(this, e);

    public override string ToString() =>
        query.ToString() ?? string.Empty;

    public bool IsSynchronized =>
        query.IsSynchronized;

    public object SyncRoot =>
        query.SyncRoot;

    public bool Contains(TElement item) =>
        query.Contains(item);

    public void CopyTo(Array array, int index) =>
        query.CopyTo(array, index);

    public void CopyTo(TElement[] array, int arrayIndex) =>
        query.CopyTo(array, arrayIndex);

    public IEnumerator<TElement> GetEnumerator() =>
        query.GetEnumerator();

    public int IndexOf(TElement item) =>
        query.IndexOf(item);

    object? IList.this[int index]
    {
        get => ((IList)query)[index];
        set => ((IList)query)[index] = value;
    }

    TElement IList<TElement>.this[int index]
    {
        get => ((IList<TElement>)query)[index];
        set => ((IList<TElement>)query)[index] = value;
    }

    int ICollection.Count =>
        query.Count;

    int IReadOnlyCollection<TElement>.Count =>
        query.Count;

    bool IList.IsFixedSize =>
        ((IList)query).IsFixedSize;

    bool IList.IsReadOnly =>
        ((IList)query).IsReadOnly;

    bool ICollection<TElement>.IsReadOnly =>
        ((ICollection<TElement>)query).IsReadOnly;

    bool IList.Contains(object? value) =>
        ((IList)query).Contains(value);

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)query).GetEnumerator();

    int IList.IndexOf(object? value) =>
        ((IList)query).IndexOf(value);

    int IList.Add(object? value) =>
        ((IList)query).Add(value);

    void ICollection<TElement>.Add(TElement item) =>
        ((ICollection<TElement>)query).Add(item);

    void IList.Clear() =>
        ((IList)query).Clear();

    void ICollection<TElement>.Clear() =>
        ((ICollection<TElement>)query).Clear();

    void IList.Insert(int index, object? value) =>
        ((IList)query).Insert(index, value);

    void IList<TElement>.Insert(int index, TElement item) =>
        ((IList<TElement>)query).Insert(index, item);

    void IList.Remove(object? value) =>
        ((IList)query).Remove(value);

    bool ICollection<TElement>.Remove(TElement item) =>
        ((ICollection<TElement>)query).Remove(item);

    void IList.RemoveAt(int index) =>
        ((IList)query).RemoveAt(index);

    void IList<TElement>.RemoveAt(int index) =>
        ((IList<TElement>)query).RemoveAt(index);

    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) =>
        query.ObserveAggregate<TAccumulate, TResult>(seedFactory, func, resultSelector);

    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveAll(predicate);

    public IObservableScalarQuery<bool> ObserveAny() =>
        query.ObserveAny();

    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveAny(predicate);

    public IObservableCollectionQuery<TElement> ObserveAppend(TElement element) =>
        query.ObserveAppend(element);

    public IObservableScalarQuery<TElement> ObserveAverage() =>
        query.ObserveAverage();

    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TElement, TResult>> selector) =>
        query.ObserveAverage<TResult>(selector);

    public IObservableCollectionQuery<TResult> ObserveCast<TResult>() =>
        query.ObserveCast<TResult>();

    public IObservableCollectionQuery<TElement> ObserveConcat(IObservableCollectionQuery<TElement> second) =>
        query.ObserveConcat(second);

    public IObservableScalarQuery<int> ObserveCount() =>
        query.ObserveCount();

    public IObservableScalarQuery<int> ObserveCount(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveCount(predicate);

    public IObservableCollectionQuery<TElement> ObserveDistinct() =>
        query.ObserveDistinct();

    public IObservableCollectionQuery<TElement> ObserveDistinct(IEqualityComparer<TElement> comparer) =>
        query.ObserveDistinct(comparer);

    public IObservableScalarQuery<TElement> ObserveElementAt(int index) =>
        query.ObserveElementAt(index);

    public IObservableScalarQuery<TElement> ObserveElementAtOrDefault(int index) =>
        query.ObserveElementAtOrDefault(index);

    public IObservableScalarQuery<TElement> ObserveFirst() =>
        query.ObserveFirst();

    public IObservableScalarQuery<TElement> ObserveFirst(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveFirst(predicate);

    public IObservableScalarQuery<TElement> ObserveFirstOrDefault() =>
        query.ObserveFirstOrDefault();

    public IObservableScalarQuery<TElement> ObserveFirstOrDefault(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveFirstOrDefault(predicate);

    public IObservableCollectionQuery<TElement> ObserveIndividualChanges() =>
        query.ObserveIndividualChanges();

    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector) =>
        query.ObserveGroupBy<TKey>(keySelector);

    public IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) =>
        query.ObserveGroupBy<TKey>(keySelector, keyEqualityComparer);

    public IObservableScalarQuery<TElement> ObserveLast() =>
        query.ObserveLast();

    public IObservableScalarQuery<TElement> ObserveLast(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveLast(predicate);

    public IObservableScalarQuery<TElement> ObserveLastOrDefault() =>
        query.ObserveLastOrDefault();

    public IObservableScalarQuery<TElement> ObserveLastOrDefault(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveLastOrDefault(predicate);

    public IObservableScalarQuery<TElement> ObserveMax() =>
        query.ObserveMax();

    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TElement, TResult>> selector) =>
        query.ObserveMax<TResult>(selector);

    public IObservableScalarQuery<TElement> ObserveMin() =>
        query.ObserveMin();

    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TElement, TResult>> selector) =>
        query.ObserveMin<TResult>(selector);

    public IObservableCollectionQuery<TResult> ObserveOfType<TResult>() =>
        query.ObserveOfType<TResult>();

    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector) =>
        query.ObserveOrderBy(selector);

    public IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector, bool isDescending) =>
        query.ObserveOrderBy(selector, isDescending);

    public IObservableCollectionQuery<TElement> ObserveOrderBy(params (Expression<Func<TElement, IComparable>> selector, bool isDescending)[] selectorsAndDirections) =>
        query.ObserveOrderBy(selectorsAndDirections);

    public IObservableCollectionQuery<TElement> ObservePrepend(TElement element) =>
        query.ObservePrepend(element);

    public IObservableCollectionQuery<TResult> ObserveSelect<TResult>(Expression<Func<TElement, TResult>> selector) =>
        query.ObserveSelect<TResult>(selector);

    public IObservableCollectionQuery<TResult> ObserveSelectMany<TResult>(Expression<Func<TElement, IEnumerable<TResult>>> selector) =>
        query.ObserveSelectMany<TResult>(selector);

    public IObservableScalarQuery<TElement> ObserveSingle() =>
        query.ObserveSingle();

    public IObservableScalarQuery<TElement> ObserveSingle(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveSingle(predicate);

    public IObservableScalarQuery<TElement> ObserveSingleOrDefault() =>
        query.ObserveSingleOrDefault();

    public IObservableScalarQuery<TElement> ObserveSingleOrDefault(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveSingleOrDefault(predicate);

    public IObservableCollectionQuery<TElement> ObserveSkip(int count) =>
        query.ObserveSkip(count);

    public IObservableCollectionQuery<TElement> ObserveSlice(Range range) =>
        query.ObserveSlice(range);

    public IObservableScalarQuery<TElement> ObserveSum() =>
        query.ObserveSum();

    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TElement, TResult>> selector) =>
        query.ObserveSum<TResult>(selector);

    public IObservableCollectionQuery<TElement> ObserveTake(int count) =>
        query.ObserveTake(count);

    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector) where TKey : notnull =>
        query.ObserveToDictionary<TKey>(keySelector);

    public IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> equalityComparer) where TKey : notnull =>
        query.ObserveToDictionary<TKey>(keySelector, equalityComparer);

    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector) where TKey : notnull =>
        query.ObserveToDictionary<TKey, TValue>(keySelector, valueSelector);

    public IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer) where TKey : notnull =>
        query.ObserveToDictionary<TKey, TValue>(keySelector, valueSelector, equalityComparer);

    public IObservableLookupQuery<TKey, TElement> ObserveToLookup<TKey>(Expression<Func<TElement, TKey>> keySelector) where TKey : notnull =>
        query.ObserveToLookup<TKey>(keySelector);

    public IObservableLookupQuery<TKey, TElement> ObserveToLookup<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) where TKey : notnull =>
        query.ObserveToLookup<TKey>(keySelector, keyEqualityComparer);

    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallback(object context, CollectionSynchronizationCallback synchronizationCallback) =>
        query.ObserveUsingSynchronizationCallback(context, synchronizationCallback);

    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallbackEventually(object context, CollectionSynchronizationCallback synchronizationCallback) =>
        query.ObserveUsingSynchronizationCallbackEventually(context, synchronizationCallback);

    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext) =>
        query.ObserveUsingSynchronizationContext(synchronizationContext);

    public IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext) =>
        query.ObserveUsingSynchronizationContextEventually(synchronizationContext);

    public IObservableCollectionQuery<TElement> ObserveUsingSyncRoot(object lockObject) =>
        query.ObserveUsingSyncRoot(lockObject);

    public IObservableCollectionQuery<TElement> ObserveUsingSyncRootEventually(object lockObject) =>
        query.ObserveUsingSyncRootEventually(lockObject);

    public IObservableCollectionQuery<TElement> ObserveWhere(Expression<Func<TElement, bool>> predicate) =>
        query.ObserveWhere(predicate);

    public TElement this[int index] =>
        query[index];

    public int Count =>
        query.Count;

    public Exception? OperationFault =>
        query.OperationFault;

    void IObservableRangeCollection<TElement>.AddRange(IEnumerable<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).AddRange(items);

    void IObservableRangeCollection<TElement>.AddRange(IList<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).AddRange(items);

    IReadOnlyList<TElement> IObservableRangeCollection<TElement>.GetAndRemoveAll(Func<TElement, bool> predicate) =>
        ((IObservableRangeCollection<TElement>)query).GetAndRemoveAll(predicate);

    TElement IObservableRangeCollection<TElement>.GetAndRemoveAt(int index) =>
        ((IObservableRangeCollection<TElement>)query).GetAndRemoveAt(index);

    public IReadOnlyList<TElement> GetRange(int index, int count) =>
        query.GetRange(index, count);

    void IObservableRangeCollection<TElement>.InsertRange(int index, IEnumerable<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).InsertRange(index, items);

    void IObservableRangeCollection<TElement>.InsertRange(int index, IList<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).InsertRange(index, items);

    void IObservableRangeCollection<TElement>.MoveRange(int oldStartIndex, int newStartIndex, int count) =>
        ((IObservableRangeCollection<TElement>)query).MoveRange(oldStartIndex, newStartIndex, count);

    int IObservableRangeCollection<TElement>.RemoveAll(Func<TElement, bool> predicate) =>
        ((IObservableRangeCollection<TElement>)query).RemoveAll(predicate);

    void IObservableRangeCollection<TElement>.RemoveRange(IEnumerable<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).RemoveRange(items);

    void IObservableRangeCollection<TElement>.RemoveRange(IList<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).RemoveRange(items);

    void IObservableRangeCollection<TElement>.RemoveRange(int index, int count) =>
        ((IObservableRangeCollection<TElement>)query).RemoveRange(index, count);

    void IObservableRangeCollection<TElement>.ReplaceAll(IEnumerable<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).ReplaceAll(items);

    void IObservableRangeCollection<TElement>.ReplaceAll(IList<TElement> items) =>
        ((IObservableRangeCollection<TElement>)query).ReplaceAll(items);

    IReadOnlyList<TElement> IObservableRangeCollection<TElement>.ReplaceRange(int index, int count, IEnumerable<TElement>? collection) =>
        ((IObservableRangeCollection<TElement>)query).ReplaceRange(index, count, collection);

    IReadOnlyList<TElement> IObservableRangeCollection<TElement>.ReplaceRange(int index, int count, IList<TElement> list) =>
        ((IObservableRangeCollection<TElement>)query).ReplaceRange(index, count, list);

    void IObservableRangeCollection<TElement>.Reset(IEnumerable<TElement> newCollection) =>
        ((IObservableRangeCollection<TElement>)query).Reset(newCollection);
}
