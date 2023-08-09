namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <inheritdoc/>
public class CollectionObserver :
    ICollectionObserver
{
    /// <summary>
    /// Initializes a new instance of <see cref="CollectionObserver"/>
    /// </summary>
    public CollectionObserver() :
        this(new ExpressionObserver())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CollectionObserver"/> using the specified <paramref name="expressionObserver"/> for predicates, selectors, and transforms
    /// </summary>
    /// <param name="expressionObserver">The expression observer to use for predicates, selectors, and transforms</param>
    public CollectionObserver(IExpressionObserver expressionObserver)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(expressionObserver);
#else
        if (expressionObserver is null)
            throw new ArgumentNullException(nameof(expressionObserver));
#endif
        ExpressionObserver = expressionObserver;
    }

    readonly Dictionary<IEnumerable, ObservableQuery> cachedEnumerableObservableCollections = new();
    readonly object cachedEnumerableObservableCollectionsAccess = new();
    readonly Dictionary<IEnumerable, ObservableQuery> cachedGenericEnumerableObservableCollections = new();
    readonly object cachedGenericEnumerableObservableCollectionsAccess = new();
    readonly Dictionary<IEnumerable, ObservableQuery> cachedGenericListObservableCollections = new();
    readonly object cachedGenericListObservableCollectionsAccess = new();
    readonly Dictionary<IEnumerable, ObservableQuery> cachedListObservableCollections = new();
    readonly object cachedListObservableCollectionsAccess = new();
    readonly Dictionary<IEnumerable, ObservableQuery> cachedReadOnlyDictionaryObservableDictionaries = new();
    readonly object cachedReadOnlyDictionaryObservableDictionariesAccess = new();
    readonly Dictionary<IEnumerable, ObservableQuery> cachedReadOnlyListObservableCollections = new();
    readonly object cachedReadOnlyListObservableCollectionsAccess = new();

    /// <inheritdoc/>
    public int CachedObservableQueries
    {
        get
        {
            var count = 0;
            lock (cachedEnumerableObservableCollectionsAccess)
                count += cachedEnumerableObservableCollections.Values.Sum(query => 1 + query.CachedObservableQueries);
            lock (cachedGenericEnumerableObservableCollectionsAccess)
                count += cachedGenericEnumerableObservableCollections.Values.Sum(query => 1 + query.CachedObservableQueries);
            lock (cachedGenericListObservableCollectionsAccess)
                count += cachedGenericListObservableCollections.Values.Sum(query => 1 + query.CachedObservableQueries);
            lock (cachedListObservableCollectionsAccess)
                count += cachedListObservableCollections.Values.Sum(query => 1 + query.CachedObservableQueries);
            lock (cachedReadOnlyDictionaryObservableDictionariesAccess)
                count += cachedReadOnlyDictionaryObservableDictionaries.Values.Sum(query => 1 + query.CachedObservableQueries);
            lock (cachedReadOnlyListObservableCollectionsAccess)
                count += cachedReadOnlyListObservableCollections.Values.Sum(query => 1 + query.CachedObservableQueries);
            return count;
        }
    }

    /// <inheritdoc/>
    public IExpressionObserver ExpressionObserver { get; }

    /// <inheritdoc/>
    public IObservableCollectionQuery<object?> ObserveEnumerable(IEnumerable enumerable)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(enumerable);
#else
        if (enumerable is null)
            throw new ArgumentNullException(nameof(enumerable));
#endif
        ObservableCollectionQueryEnumerable enumerableObservableCollection;
        lock (cachedEnumerableObservableCollectionsAccess)
        {
            if (!cachedEnumerableObservableCollections.TryGetValue(enumerable, out var cachedCollectionObservableCollection))
            {
                enumerableObservableCollection = new ObservableCollectionQueryEnumerable(this, enumerable);
                cachedEnumerableObservableCollections.Add(enumerable, enumerableObservableCollection);
            }
            else
                enumerableObservableCollection = (ObservableCollectionQueryEnumerable)cachedCollectionObservableCollection;
            ++enumerableObservableCollection.Observations;
        }
        enumerableObservableCollection.Initialize();
        return enumerableObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<object?> ObserveList(IList list)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
#else
        if (list is null)
            throw new ArgumentNullException(nameof(list));
#endif
        ObservableCollectionQueryList listObservableCollection;
        lock (cachedListObservableCollectionsAccess)
        {
            if (!cachedListObservableCollections.TryGetValue(list, out var cachedCollectionObservableCollection))
            {
                listObservableCollection = new ObservableCollectionQueryList(this, list);
                cachedListObservableCollections.Add(list, listObservableCollection);
            }
            else
                listObservableCollection = (ObservableCollectionQueryList)cachedCollectionObservableCollection;
            ++listObservableCollection.Observations;
        }
        listObservableCollection.Initialize();
        return listObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveEnumerable<TElement>(IEnumerable<TElement> enumerable)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(enumerable);
#else
        if (enumerable is null)
            throw new ArgumentNullException(nameof(enumerable));
#endif
        ObservableCollectionQueryGenericEnumerable<TElement> enumerableObservableCollection;
        lock (cachedGenericEnumerableObservableCollectionsAccess)
        {
            if (!cachedGenericEnumerableObservableCollections.TryGetValue(enumerable, out var cachedEnumerableObservableCollection))
            {
                enumerableObservableCollection = new ObservableCollectionQueryGenericEnumerable<TElement>(this, enumerable);
                cachedGenericEnumerableObservableCollections.Add(enumerable, enumerableObservableCollection);
            }
            else
                enumerableObservableCollection = (ObservableCollectionQueryGenericEnumerable<TElement>)cachedEnumerableObservableCollection;
            ++enumerableObservableCollection.Observations;
        }
        enumerableObservableCollection.Initialize();
        return enumerableObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveList<TElement>(IList<TElement> list)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(list);
#else
        if (list is null)
            throw new ArgumentNullException(nameof(list));
#endif
        ObservableCollectionQueryGenericList<TElement> listObservableCollection;
        lock (cachedGenericListObservableCollectionsAccess)
        {
            if (!cachedGenericListObservableCollections.TryGetValue(list, out var cachedListObservableCollection))
            {
                listObservableCollection = new ObservableCollectionQueryGenericList<TElement>(this, list);
                cachedGenericListObservableCollections.Add(list, listObservableCollection);
            }
            else
                listObservableCollection = (ObservableCollectionQueryGenericList<TElement>)cachedListObservableCollection;
            ++listObservableCollection.Observations;
        }
        listObservableCollection.Initialize();
        return listObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveReadOnlyList<TElement>(IReadOnlyList<TElement> readOnlyList)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(readOnlyList);
#else
        if (readOnlyList is null)
            throw new ArgumentNullException(nameof(readOnlyList));
#endif
        ObservableCollectionQueryReadOnlyList<TElement> readOnlyListObservableCollection;
        lock (cachedReadOnlyListObservableCollectionsAccess)
        {
            if (!cachedReadOnlyListObservableCollections.TryGetValue(readOnlyList, out var cachedReadOnlyListObservableCollection))
            {
                readOnlyListObservableCollection = new ObservableCollectionQueryReadOnlyList<TElement>(this, readOnlyList);
                cachedReadOnlyListObservableCollections.Add(readOnlyList, readOnlyListObservableCollection);
            }
            else
                readOnlyListObservableCollection = (ObservableCollectionQueryReadOnlyList<TElement>)cachedReadOnlyListObservableCollection;
            ++readOnlyListObservableCollection.Observations;
        }
        readOnlyListObservableCollection.Initialize();
        return readOnlyListObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableDictionaryQuery<TKey, TValue> ObserveReadOnlyDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dictionary);
#else
        if (dictionary is null)
            throw new ArgumentNullException(nameof(dictionary));
#endif
        ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue> readOnlyDictionaryObservableDictionary;
        lock (cachedReadOnlyDictionaryObservableDictionariesAccess)
        {
            if (!cachedReadOnlyDictionaryObservableDictionaries.TryGetValue(dictionary, out var cachedReadOnlyDictionaryObservableDictionary))
            {
                readOnlyDictionaryObservableDictionary = new ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>(this, dictionary);
                cachedReadOnlyDictionaryObservableDictionaries.Add(dictionary, readOnlyDictionaryObservableDictionary);
            }
            else
                readOnlyDictionaryObservableDictionary = (ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>)cachedReadOnlyDictionaryObservableDictionary;
            ++readOnlyDictionaryObservableDictionary.Observations;
        }
        readOnlyDictionaryObservableDictionary.Initialize();
        return readOnlyDictionaryObservableDictionary;
    }

    internal bool QueryDisposed(ObservableCollectionQueryEnumerable enumerableObservableCollection)
    {
        lock (cachedEnumerableObservableCollectionsAccess)
        {
            if (--enumerableObservableCollection.Observations == 0)
            {
                cachedEnumerableObservableCollections.Remove(enumerableObservableCollection.Enumerable);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed(ObservableCollectionQueryList listObservableCollection)
    {
        lock (cachedListObservableCollectionsAccess)
        {
            if (--listObservableCollection.Observations == 0)
            {
                cachedListObservableCollections.Remove(listObservableCollection.List);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TElement>(ObservableCollectionQueryGenericEnumerable<TElement> enumerableObservableCollection)
    {
        lock (cachedGenericEnumerableObservableCollectionsAccess)
        {
            if (--enumerableObservableCollection.Observations == 0)
            {
                cachedGenericEnumerableObservableCollections.Remove(enumerableObservableCollection.Enumerable);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TElement>(ObservableCollectionQueryGenericList<TElement> listObservableCollection)
    {
        lock (cachedGenericListObservableCollectionsAccess)
        {
            if (--listObservableCollection.Observations == 0)
            {
                cachedGenericListObservableCollections.Remove(listObservableCollection.List);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TElement>(ObservableCollectionQueryReadOnlyList<TElement> readOnlyListObservableCollection)
    {
        lock (cachedReadOnlyListObservableCollectionsAccess)
        {
            if (--readOnlyListObservableCollection.Observations == 0)
            {
                cachedReadOnlyListObservableCollections.Remove(readOnlyListObservableCollection.ReadOnlyList);
                return true;
            }
        }
        return false;
    }

    internal bool QueryDisposed<TKey, TValue>(ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue> readOnlyDictionaryObservableDictionary)
        where TKey : notnull
    {
        lock (cachedReadOnlyDictionaryObservableDictionariesAccess)
        {
            if (--readOnlyDictionaryObservableDictionary.Observations == 0)
            {
                cachedReadOnlyDictionaryObservableDictionaries.Remove(readOnlyDictionaryObservableDictionary.ReadOnlyDictionary);
                return true;
            }
        }
        return false;
    }
}
