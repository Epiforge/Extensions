namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionGroupByQuery<TKey, TElement> :
    ObservableCollectionQuery<IObservableGrouping<TKey, TElement>>
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, TKey>>, Expression<Func<TElement, Tuple<TElement, TKey>>>> cachedWrappedSelectors = new(ExpressionEqualityComparer.Default);

    static Expression<Func<TElement, Tuple<TElement, TKey>>> CachedWrappedSelectorsValueFactory(Expression<Func<TElement, TKey>> selector)
    {
        var parameter = Expression.Parameter(typeof(TElement), "element");
        return Expression.Lambda<Func<TElement, Tuple<TElement, TKey>>>(Expression.New(typeof(Tuple<TElement, TKey>).GetConstructor(new[] { typeof(TElement), typeof(TKey) })!, parameter, Expression.Invoke(selector, parameter)), parameter);
    }

    public ObservableCollectionGroupByQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer) :
        base(collectionObserver)
    {
        this.source = source;
        KeySelector = keySelector;
        KeyEqualityComparer = keyEqualityComparer;
        access = new();
        collectionAndGroupingByKey = new(KeyEqualityComparer);
        groupings = new();
    }

    readonly object access;
    readonly NullableKeyDictionary<TKey, (RangeObservableCollection<TElement> collection, IObservableGrouping<TKey, TElement> grouping)> collectionAndGroupingByKey;
    readonly RangeObservableCollection<IObservableGrouping<TKey, TElement>> groupings;
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

    public override int Count
    {
        get
        {
            lock (access)
                return groupings.Count;
        }
    }

    void AddElement(TElement element, TKey key)
    {
        RangeObservableCollection<TElement> collection;
        if (!collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            collection = new RangeObservableCollection<TElement>();
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

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
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

    void RemoveElement(TElement element, TKey key)
    {
        if (collectionAndGroupingByKey.TryGetValue(key, out var collectionAndGrouping))
        {
            var collection = collectionAndGrouping.collection;
            collection.Remove(element);
            if (collection.Count == 0)
            {
                collectionAndGroupingByKey.Remove(key);
                var grouping = collectionAndGrouping.grouping;
                groupings.Remove(grouping);
                ((ObservableGrouping<TKey, TElement>)grouping).InternalDispose();
            }
        }
    }

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
        if (e.PropertyName == nameof(IObservableCollectionQuery<Tuple<TElement, TKey>>.OperationFault))
            OperationFault = select!.OperationFault;
    }
}
