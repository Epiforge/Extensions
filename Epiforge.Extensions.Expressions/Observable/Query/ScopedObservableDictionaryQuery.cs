namespace Epiforge.Extensions.Expressions.Observable.Query;

class ScopedObservableDictionaryQuery<TKey, TValue> :
    IObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    internal ScopedObservableDictionaryQuery(ObservableDictionaryQuery<TKey, TValue> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        this.query = query;
        this.query.PropertyChanged += QueryPropertyChanged;
        this.query.PropertyChanging += QueryPropertyChanging;
        this.query.CollectionChanged += QueryCollectionChanged;
        this.query.DictionaryChanged += QueryDictionaryChanged;
        ((INotifyDictionaryChanged)this.query).DictionaryChanged += QueryDictionaryChangedBoxed;
    }

    internal readonly ObservableDictionaryQuery<TKey, TValue> query;
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

    public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? DictionaryChanged;

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => boxedDictionaryChanged += value;
        remove => boxedDictionaryChanged -= value;
    }

    EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? boxedDictionaryChanged;

    public event EventHandler<DisposalNotificationEventArgs>? Disposed;

    public event EventHandler<DisposalNotificationEventArgs>? Disposing;

    event EventHandler<DisposalNotificationEventArgs>? INotifyDisposalOverridden.DisposalOverridden
    {
        add { }
        remove { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = DisposalNotificationEventArgs.ByCallingDispose;
        Disposing?.Invoke(this, e);
        query.PropertyChanged -= QueryPropertyChanged;
        query.PropertyChanging -= QueryPropertyChanging;
        query.CollectionChanged -= QueryCollectionChanged;
        query.DictionaryChanged -= QueryDictionaryChanged;
        ((INotifyDictionaryChanged)query).DictionaryChanged -= QueryDictionaryChangedBoxed;
        query.Dispose();
        Disposed?.Invoke(this, e);
    }

    void QueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    void QueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        DictionaryChanged?.Invoke(this, e);

    void QueryDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        boxedDictionaryChanged?.Invoke(this, e);

    void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        PropertyChanging?.Invoke(this, e);

    public override string ToString() =>
        query.ToString() ?? string.Empty;

    public bool IsReadOnly =>
        query.IsReadOnly;

    public bool IsSynchronized =>
        query.IsSynchronized;

    public object SyncRoot =>
        query.SyncRoot;

    public IEnumerable<TKey> Keys =>
        query.Keys;

    public IEnumerable<TValue> Values =>
        query.Values;

    public bool ContainsKey(TKey key) =>
        query.ContainsKey(key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        query.CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        query.GetEnumerator();

    public IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys) =>
        query.GetRange(keys);

    public bool TryGetValue(TKey key, out TValue value) =>
        query.TryGetValue(key, out value);

    public TValue this[TKey key] =>
        query[key];

    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        get => ((IDictionary<TKey, TValue>)query)[key];
        set => ((IDictionary<TKey, TValue>)query)[key] = value;
    }

    TValue IRangeDictionary<TKey, TValue>.this[TKey key]
    {
        get => ((IRangeDictionary<TKey, TValue>)query)[key];
        set => ((IRangeDictionary<TKey, TValue>)query)[key] = value;
    }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys =>
        ((IDictionary<TKey, TValue>)query).Keys;

    ICollection<TValue> IDictionary<TKey, TValue>.Values =>
        ((IDictionary<TKey, TValue>)query).Values;

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)query).GetEnumerator();

    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) =>
        ((IDictionary<TKey, TValue>)query).Add(key, value);

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)query).Add(item);

    void ICollection<KeyValuePair<TKey, TValue>>.Clear() =>
        ((ICollection<KeyValuePair<TKey, TValue>>)query).Clear();

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)query).Contains(item);

    bool IDictionary<TKey, TValue>.Remove(TKey key) =>
        ((IDictionary<TKey, TValue>)query).Remove(key);

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)query).Remove(item);

    void IRangeDictionary<TKey, TValue>.AddRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) =>
        ((IRangeDictionary<TKey, TValue>)query).AddRange(keyValuePairs);

    void IRangeDictionary<TKey, TValue>.AddRange(IReadOnlyList<KeyValuePair<TKey, TValue>> keyValuePairs) =>
        ((IRangeDictionary<TKey, TValue>)query).AddRange(keyValuePairs);

    IReadOnlyList<KeyValuePair<TKey, TValue>> IRangeDictionary<TKey, TValue>.RemoveAll(Func<TKey, TValue, bool> predicate) =>
        ((IRangeDictionary<TKey, TValue>)query).RemoveAll(predicate);

    IReadOnlyList<TKey> IRangeDictionary<TKey, TValue>.RemoveRange(IEnumerable<TKey> keys) =>
        ((IRangeDictionary<TKey, TValue>)query).RemoveRange(keys);

    void IRangeDictionary<TKey, TValue>.ReplaceRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs) =>
        ((IRangeDictionary<TKey, TValue>)query).ReplaceRange(keyValuePairs);

    IReadOnlyList<TKey> IRangeDictionary<TKey, TValue>.ReplaceRange(IEnumerable<TKey> removeKeys, IEnumerable<KeyValuePair<TKey, TValue>> newKeyValuePairs) =>
        ((IRangeDictionary<TKey, TValue>)query).ReplaceRange(removeKeys, newKeyValuePairs);

    void IRangeDictionary<TKey, TValue>.Reset() =>
        ((IRangeDictionary<TKey, TValue>)query).Reset();

    void IRangeDictionary<TKey, TValue>.Reset(IDictionary<TKey, TValue> dictionary) =>
        ((IRangeDictionary<TKey, TValue>)query).Reset(dictionary);

    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) =>
        query.ObserveAggregate<TAccumulate, TResult>(seedFactory, func, resultSelector);

    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveAll(predicate);

    public IObservableScalarQuery<bool> ObserveAny() =>
        query.ObserveAny();

    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveAny(predicate);

    public IObservableScalarQuery<TValue> ObserveAverage() =>
        query.ObserveAverage();

    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector) =>
        query.ObserveAverage<TResult>(selector);

    public IObservableDictionaryQuery<TKey, TResult> ObserveCast<TResult>() =>
        query.ObserveCast<TResult>();

    public IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently() =>
        query.ObserveConcurrently();

    public IObservableScalarQuery<int> ObserveCount() =>
        query.ObserveCount();

    public IObservableScalarQuery<int> ObserveCount(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveCount(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst() =>
        query.ObserveFirst();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveFirst(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(IComparer<TKey> comparer) =>
        query.ObserveFirst(comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer) =>
        query.ObserveFirst(predicate, comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault() =>
        query.ObserveFirstOrDefault();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveFirstOrDefault(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(IComparer<TKey> comparer) =>
        query.ObserveFirstOrDefault(comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer) =>
        query.ObserveFirstOrDefault(predicate, comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast() =>
        query.ObserveLast();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveLast(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> comparer) =>
        query.ObserveLast(comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer) =>
        query.ObserveLast(predicate, comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault() =>
        query.ObserveLastOrDefault();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveLastOrDefault(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> comparer) =>
        query.ObserveLastOrDefault(comparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer) =>
        query.ObserveLastOrDefault(predicate, comparer);

    public IObservableScalarQuery<TValue> ObserveMax() =>
        query.ObserveMax();

    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector) =>
        query.ObserveMax<TResult>(selector);

    public IObservableScalarQuery<TValue> ObserveMin() =>
        query.ObserveMin();

    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector) =>
        query.ObserveMin<TResult>(selector);

    public IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>() =>
        query.ObserveOfType<TResult>();

    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector) where TResultKey : notnull =>
        query.ObserveSelect<TResultKey, TResultValue>(keySelector, valueSelector);

    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector, IEqualityComparer<TResultKey> equalityComparer) where TResultKey : notnull =>
        query.ObserveSelect<TResultKey, TResultValue>(keySelector, valueSelector, equalityComparer);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle() =>
        query.ObserveSingle();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveSingle(predicate);

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault() =>
        query.ObserveSingleOrDefault();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveSingleOrDefault(predicate);

    public IObservableScalarQuery<TValue> ObserveSum() =>
        query.ObserveSum();

    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector) =>
        query.ObserveSum<TResult>(selector);

    public IObservableCollectionQuery<TValue> ObserveToCollection() =>
        query.ObserveToCollection();

    public IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Expression<Func<TKey, TValue, TElement>> selector) =>
        query.ObserveToCollection<TElement>(selector);

    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext) =>
        query.ObserveUsingSynchronizationContext(synchronizationContext);

    public IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext) =>
        query.ObserveUsingSynchronizationContextEventually(synchronizationContext);

    public IObservableScalarQuery<TValue> ObserveValueFor(TKey key) =>
        query.ObserveValueFor(key);

    public IObservableScalarQuery<TValue> ObserveValueForOrDefault(TKey key) =>
        query.ObserveValueForOrDefault(key);

    public IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate) =>
        query.ObserveWhere(predicate);

    public int Count =>
        query.Count;

    public Exception? OperationFault =>
        query.OperationFault;
}
