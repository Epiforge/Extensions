namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionLookupQuery<TKey, TElement> :
    ObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IObservableLookupQuery<TKey, TElement>
    where TKey : notnull
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, TKey>>, Expression<Func<TElement, Tuple<TElement, TKey>>>> cachedWrappedSelectors = new(ExpressionEqualityComparer.Default);

    static Expression<Func<TElement, Tuple<TElement, TKey>>> CachedWrappedSelectorsValueFactory(Expression<Func<TElement, TKey>> selector)
    {
        var parameter = Expression.Parameter(typeof(TElement), "element");
        return Expression.Lambda<Func<TElement, Tuple<TElement, TKey>>>(Expression.New(typeof(Tuple<TElement, TKey>).GetConstructor([typeof(TElement), typeof(TKey)])!, parameter, Expression.Invoke(selector, parameter)), parameter);
    }

    public ObservableCollectionLookupQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) :
        base(collectionObserver)
    {
        this.source = source;
        KeySelector = keySelector;
        KeyEqualityComparer = keyEqualityComparer;
        access = new();
        collectionAndGroupingByKey = new(KeyEqualityComparer);
        groupings = [];
        groupingsQuery = this.collectionObserver.ObserveReadOnlyList(groupings);
        groupingByKey = groupingsQuery.ObserveToDictionary(g => g.Key);
    }

    readonly object access;
    readonly Dictionary<TKey, (ObservableRangeCollection<TElement> collection, IObservableGrouping<TKey, TElement> grouping)> collectionAndGroupingByKey;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    readonly IObservableDictionaryQuery<TKey, IObservableGrouping<TKey, TElement>> groupingByKey;
    readonly ObservableRangeCollection<IObservableGrouping<TKey, TElement>> groupings;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    readonly IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> groupingsQuery;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<Tuple<TElement, TKey>>? select;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly Expression<Func<TElement, TKey>> KeySelector;
    internal readonly IEqualityComparer<TKey> KeyEqualityComparer;

    public override IObservableGrouping<TKey, TElement> this[int index]
    {
        get
        {
            lock (access)
                return groupings[index];
        }
    }

    public IObservableGrouping<TKey, TElement> this[TKey key]
    {
        get
        {
            lock (access)
            {
                if (groupingByKey.TryGetValue(key, out var existingGrouping))
                    return existingGrouping;
                var collection = collectionObserver.ExpressionObserver.Logger is { } logger ? new ObservableRangeCollection<TElement>(logger) : new ObservableRangeCollection<TElement>();
                var grouping = new ObservableGrouping<TKey, TElement>(collectionObserver, key, (ObservableCollectionQuery<TElement>)collectionObserver.ObserveReadOnlyList(collection));
                grouping.Initialize();
                var collectionAndGrouping = (collection, grouping);
                collectionAndGroupingByKey.Add(key, collectionAndGrouping);
                groupings.Add(grouping);
                return grouping;
            }
        }
    }

    IObservableGrouping<TKey, TElement> IDictionary<TKey, IObservableGrouping<TKey, TElement>>.this[TKey key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    IObservableGrouping<TKey, TElement> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.this[TKey key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override int Count
    {
        get
        {
            lock (access)
                return groupings.Count;
        }
    }

    public bool IsReadOnly =>
        true;

    public ICollection<TKey> Keys
    {
        get
        {
            lock (access)
                return groupingByKey.Keys.ToList().AsReadOnly();
        }
    }

    IEnumerable<TKey> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        Keys;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Keys =>
        Keys;

    public ICollection<IObservableGrouping<TKey, TElement>> Values
    {
        get
        {
            lock (access)
                return groupingByKey.Values.ToList().AsReadOnly();

        }
    }

    IEnumerable<IObservableGrouping<TKey, TElement>> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        Values;

    IEnumerable<IObservableGrouping<TKey, TElement>> IReadOnlyDictionary<TKey, IObservableGrouping<TKey, TElement>>.Values =>
        Values;

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => ((INotifyDictionaryChanged)groupingByKey).DictionaryChanged += value;
        remove => ((INotifyDictionaryChanged)groupingByKey).DictionaryChanged -= value;
    }

    event EventHandler<NotifyDictionaryChangedEventArgs<TKey, IObservableGrouping<TKey, TElement>>>? INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>.DictionaryChanged
    {
        add => ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)groupingByKey).DictionaryChanged += value;
        remove => ((INotifyDictionaryChanged<TKey, IObservableGrouping<TKey, TElement>>)groupingByKey).DictionaryChanged -= value;
    }

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Add(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    void IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Add(TKey key, IObservableGrouping<TKey, TElement> value) =>
        throw new NotSupportedException();

    void AddElement(TElement element, TKey key)
    {
        ObservableRangeCollection<TElement> collection;
        if (!collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            collection = collectionObserver.ExpressionObserver.Logger is { } logger ? new(logger) : new();
            var grouping = new ObservableGrouping<TKey, TElement>(collectionObserver, key, (ObservableCollectionQuery<TElement>)collectionObserver.ObserveReadOnlyList(collection));
            grouping.Initialize();
            collectionAndGrouping = (collection, grouping);
            collectionAndGroupingByKey.Add(key, collectionAndGrouping);
            groupings.Add(grouping);
        }
        else
            collection = collectionAndGrouping.collection;
        collection.Add(element);
    }

    void IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.AddRange(IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> keyValuePairs) =>
        throw new NotSupportedException();

    void IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.AddRange(IReadOnlyList<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> keyValuePairs) =>
        throw new NotSupportedException();

    void ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Clear() =>
        throw new NotSupportedException();

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Contains(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    public bool ContainsKey(TKey key)
    {
        lock (access)
            return groupingByKey.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>[] array, int arrayIndex)
    {
        lock (access)
            groupingByKey.CopyTo(array, arrayIndex);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
                    groupingByKey.Dispose();
                    groupingsQuery.Dispose();
                    foreach (var (_, grouping) in collectionAndGroupingByKey.Values)
                        ((ObservableGrouping<TKey, TElement>)grouping).InternalDispose();
                    if (groupings is not null)
                        groupings.CollectionChanged -= GroupingsCollectionChanged;
                    if (select is not null)
                    {
                        select.CollectionChanged -= SelectCollectionChanged;
                        select.PropertyChanged -= SelectPropertyChanged;
                        select.Dispose();
                    }
                    RemovedFromCache();
                }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<IObservableGrouping<TKey, TElement>> GetEnumerator()
    {
        lock (access)
            foreach (var grouping in groupings)
                yield return grouping;
    }

    IEnumerator<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.GetEnumerator()
    {
        lock (access)
            foreach (var kv in groupingByKey)
                yield return kv;
    }

    public IReadOnlyList<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return groupingByKey.GetRange(keys);
    }

    void GroupingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    protected override void OnInitialization()
    {
        groupings.CollectionChanged += GroupingsCollectionChanged;
        select = source.ObserveSelect(cachedWrappedSelectors.GetOrAdd(KeySelector, CachedWrappedSelectorsValueFactory));
        select.CollectionChanged += SelectCollectionChanged;
        select.PropertyChanged += SelectPropertyChanged;
        lock (access)
            foreach (var (element, key) in select)
                AddElement(element, key);
    }

    bool ICollection<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>>.Remove(KeyValuePair<TKey, IObservableGrouping<TKey, TElement>> item) =>
        throw new NotSupportedException();

    bool IDictionary<TKey, IObservableGrouping<TKey, TElement>>.Remove(TKey key) =>
        throw new NotSupportedException();

    IReadOnlyList<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.RemoveAll(Func<TKey, IObservableGrouping<TKey, TElement>, bool> predicate) =>
        throw new NotSupportedException();

    void RemoveElement(TElement element, TKey key)
    {
        if (collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            var collection = collectionAndGrouping.collection;
            collection.Remove(element);
        }
    }

    IReadOnlyList<TKey> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.RemoveRange(IEnumerable<TKey> keys) =>
        throw new NotSupportedException();

    void IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.ReplaceRange(IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> keyValuePairs) =>
        throw new NotSupportedException();

    IReadOnlyList<TKey> IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.ReplaceRange(IEnumerable<TKey> removeKeys, IEnumerable<KeyValuePair<TKey, IObservableGrouping<TKey, TElement>>> newKeyValuePairs) =>
        throw new NotSupportedException();

    void IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Reset() =>
        throw new NotSupportedException();

    void IRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>.Reset(IDictionary<TKey, IObservableGrouping<TKey, TElement>> dictionary) =>
        throw new NotSupportedException();

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                groupings.CollectionChanged -= GroupingsCollectionChanged;
                foreach (var (_, grouping) in collectionAndGroupingByKey.Values)
                    ((ObservableGrouping<TKey, TElement>)grouping).InternalDispose();
                collectionAndGroupingByKey.Clear();
                groupings.Clear();
                foreach (var (element, key) in select!)
                    AddElement(element, key);
                OnCollectionChanged(e);
                groupings.CollectionChanged += GroupingsCollectionChanged;
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } oldItems)
                    foreach (var (element, key) in oldItems.Cast<Tuple<TElement, TKey>>())
                        RemoveElement(element, key);
                if (e.NewItems is { } newItems)
                    foreach (var (element, key) in newItems.Cast<Tuple<TElement, TKey>>())
                        AddElement(element, key);
            }
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            OperationFault = select!.OperationFault;
    }

    public override string ToString() =>
        $"grouping of {source} by {KeySelector}";

    public bool TryGetValue(TKey key, out IObservableGrouping<TKey, TElement> value)
    {
        lock (access)
            return groupingByKey.TryGetValue(key, out value);
    }
}
