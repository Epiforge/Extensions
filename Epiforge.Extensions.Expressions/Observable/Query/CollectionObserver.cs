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
        ArgumentNullException.ThrowIfNull(expressionObserver);
        ExpressionObserver = expressionObserver;
        TraceLogger = expressionObserver is ExpressionObserver observer ? observer.TraceLogger : expressionObserver.Logger is { } logger ? new GuardedLogger(logger) : null;
    }

    /// <summary>
    /// Gets the logger the queries' own traces are written through, which is the expression observer's logger made unable to throw
    /// </summary>
    internal readonly ILogger? TraceLogger;

    readonly Dictionary<IEnumerable, ObservableQuery> cachedEnumerableObservableCollections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<IEnumerable, ObservableQuery> cachedGenericEnumerableObservableCollections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<IEnumerable, ObservableQuery> cachedGenericListObservableCollections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<IEnumerable, ObservableQuery> cachedListObservableCollections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<IEnumerable, ObservableQuery> cachedReadOnlyDictionaryObservableDictionaries = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<IEnumerable, ObservableQuery> cachedReadOnlyListObservableCollections = new(ReferenceEqualityComparer.Instance);
#if IS_NET_9_0_OR_GREATER
    readonly Lock cachedEnumerableObservableCollectionsAccess = new();
    readonly Lock cachedGenericEnumerableObservableCollectionsAccess = new();
    readonly Lock cachedGenericListObservableCollectionsAccess = new();
    readonly Lock cachedListObservableCollectionsAccess = new();
    readonly Lock cachedReadOnlyDictionaryObservableDictionariesAccess = new();
    readonly Lock cachedReadOnlyListObservableCollectionsAccess = new();
#else
    readonly object cachedEnumerableObservableCollectionsAccess = new();
    readonly object cachedGenericEnumerableObservableCollectionsAccess = new();
    readonly object cachedGenericListObservableCollectionsAccess = new();
    readonly object cachedListObservableCollectionsAccess = new();
    readonly object cachedReadOnlyDictionaryObservableDictionariesAccess = new();
    readonly object cachedReadOnlyListObservableCollectionsAccess = new();
#endif

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
        ArgumentNullException.ThrowIfNull(enumerable);
        ObservableCollectionQueryEnumerable enumerableObservableCollection;
        lock (cachedEnumerableObservableCollectionsAccess)
        {
            if (cachedEnumerableObservableCollections.TryGetValue(enumerable, out var cachedCollectionObservableCollection))
            {
                enumerableObservableCollection = (ObservableCollectionQueryEnumerable)cachedCollectionObservableCollection;
                ++enumerableObservableCollection.Observations;
                return enumerableObservableCollection.AsScoped();
            }
        }
        var built = new ObservableCollectionQueryEnumerable(this, enumerable);
        built.Observations = 1;
        built.Initialize();
        lock (cachedEnumerableObservableCollectionsAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedEnumerableObservableCollections, enumerable, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            enumerableObservableCollection = (ObservableCollectionQueryEnumerable)cached!;
            ++enumerableObservableCollection.Observations;
        }
        built.Release();
        return enumerableObservableCollection.AsScoped();
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<object?> ObserveList(IList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        ObservableCollectionQueryList listObservableCollection;
        lock (cachedListObservableCollectionsAccess)
        {
            if (cachedListObservableCollections.TryGetValue(list, out var cachedCollectionObservableCollection))
            {
                listObservableCollection = (ObservableCollectionQueryList)cachedCollectionObservableCollection;
                ++listObservableCollection.Observations;
                return listObservableCollection.AsScoped();
            }
        }
        var built = new ObservableCollectionQueryList(this, list);
        built.Observations = 1;
        built.Initialize();
        lock (cachedListObservableCollectionsAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedListObservableCollections, list, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            listObservableCollection = (ObservableCollectionQueryList)cached!;
            ++listObservableCollection.Observations;
        }
        built.Release();
        return listObservableCollection.AsScoped();
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveEnumerable<TElement>(IEnumerable<TElement> enumerable)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ObservableCollectionQueryGenericEnumerable<TElement> enumerableObservableCollection;
        lock (cachedGenericEnumerableObservableCollectionsAccess)
        {
            if (cachedGenericEnumerableObservableCollections.TryGetValue(enumerable, out var cachedEnumerableObservableCollection))
            {
                enumerableObservableCollection = (ObservableCollectionQueryGenericEnumerable<TElement>)cachedEnumerableObservableCollection;
                ++enumerableObservableCollection.Observations;
                return enumerableObservableCollection.AsScoped();
            }
        }
        var built = new ObservableCollectionQueryGenericEnumerable<TElement>(this, enumerable);
        built.Observations = 1;
        built.Initialize();
        lock (cachedGenericEnumerableObservableCollectionsAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedGenericEnumerableObservableCollections, enumerable, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            enumerableObservableCollection = (ObservableCollectionQueryGenericEnumerable<TElement>)cached!;
            ++enumerableObservableCollection.Observations;
        }
        built.Release();
        return enumerableObservableCollection.AsScoped();
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveList<TElement>(IList<TElement> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        ObservableCollectionQueryGenericList<TElement> listObservableCollection;
        lock (cachedGenericListObservableCollectionsAccess)
        {
            if (cachedGenericListObservableCollections.TryGetValue(list, out var cachedListObservableCollection))
            {
                listObservableCollection = (ObservableCollectionQueryGenericList<TElement>)cachedListObservableCollection;
                ++listObservableCollection.Observations;
                return listObservableCollection.AsScoped();
            }
        }
        var built = new ObservableCollectionQueryGenericList<TElement>(this, list);
        built.Observations = 1;
        built.Initialize();
        lock (cachedGenericListObservableCollectionsAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedGenericListObservableCollections, list, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            listObservableCollection = (ObservableCollectionQueryGenericList<TElement>)cached!;
            ++listObservableCollection.Observations;
        }
        built.Release();
        return listObservableCollection.AsScoped();
    }

    internal ObservableCollectionQueryReadOnlyList<TElement> GetObservableCollectionQuery<TElement>(IReadOnlyList<TElement> readOnlyList, ObservableQuery? changeLockHolder = null)
    {
        ArgumentNullException.ThrowIfNull(readOnlyList);
        ObservableCollectionQueryReadOnlyList<TElement> readOnlyListObservableCollection;
        lock (cachedReadOnlyListObservableCollectionsAccess)
        {
            if (cachedReadOnlyListObservableCollections.TryGetValue(readOnlyList, out var cachedReadOnlyListObservableCollection))
            {
                readOnlyListObservableCollection = (ObservableCollectionQueryReadOnlyList<TElement>)cachedReadOnlyListObservableCollection;
                ++readOnlyListObservableCollection.Observations;
                return readOnlyListObservableCollection;
            }
        }
        var built = new ObservableCollectionQueryReadOnlyList<TElement>(this, readOnlyList, changeLockHolder);
        built.Observations = 1;
        built.Initialize();
        lock (cachedReadOnlyListObservableCollectionsAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedReadOnlyListObservableCollections, readOnlyList, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built;
            }
            readOnlyListObservableCollection = (ObservableCollectionQueryReadOnlyList<TElement>)cached!;
            ++readOnlyListObservableCollection.Observations;
        }
        built.Release();
        return readOnlyListObservableCollection;
    }

    /// <inheritdoc/>
    public IObservableCollectionQuery<TElement> ObserveReadOnlyList<TElement>(IReadOnlyList<TElement> readOnlyList) =>
        GetObservableCollectionQuery(readOnlyList).AsScoped();

    /// <inheritdoc/>
    public IObservableDictionaryQuery<TKey, TValue> ObserveReadOnlyDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue> readOnlyDictionaryObservableDictionary;
        lock (cachedReadOnlyDictionaryObservableDictionariesAccess)
        {
            if (cachedReadOnlyDictionaryObservableDictionaries.TryGetValue(dictionary, out var cachedReadOnlyDictionaryObservableDictionary))
            {
                readOnlyDictionaryObservableDictionary = (ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>)cachedReadOnlyDictionaryObservableDictionary;
                ++readOnlyDictionaryObservableDictionary.Observations;
                return readOnlyDictionaryObservableDictionary.AsScoped();
            }
        }
        var built = new ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>(this, dictionary);
        built.Observations = 1;
        built.Initialize();
        lock (cachedReadOnlyDictionaryObservableDictionariesAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedReadOnlyDictionaryObservableDictionaries, dictionary, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return built.AsScoped();
            }
            readOnlyDictionaryObservableDictionary = (ObservableDictionaryQueryReadOnlyDictionary<TKey, TValue>)cached!;
            ++readOnlyDictionaryObservableDictionary.Observations;
        }
        built.Release();
        return readOnlyDictionaryObservableDictionary.AsScoped();
    }

    internal bool QueryDisposed(ObservableCollectionQueryEnumerable enumerableObservableCollection)
    {
        lock (cachedEnumerableObservableCollectionsAccess)
        {
            var remaining = --enumerableObservableCollection.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (enumerableObservableCollection.IsCached)
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
            var remaining = --listObservableCollection.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (listObservableCollection.IsCached)
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
            var remaining = --enumerableObservableCollection.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (enumerableObservableCollection.IsCached)
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
            var remaining = --listObservableCollection.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (listObservableCollection.IsCached)
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
            var remaining = --readOnlyListObservableCollection.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (readOnlyListObservableCollection.IsCached)
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
            var remaining = --readOnlyDictionaryObservableDictionary.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (readOnlyDictionaryObservableDictionary.IsCached)
                    cachedReadOnlyDictionaryObservableDictionaries.Remove(readOnlyDictionaryObservableDictionary.ReadOnlyDictionary);
                return true;
            }
        }
        return false;
    }
}
