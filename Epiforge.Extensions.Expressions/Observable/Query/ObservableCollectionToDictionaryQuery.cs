namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    readonly object access = new();
    readonly PrefixWeightedSequence<KeyValuePair<TKey, TValue>> claims = new();
    readonly Dictionary<TKey, List<PrefixWeightedSequenceNode<KeyValuePair<TKey, TValue>>>> claimantsByKey = new(equalityComparer);
    readonly ObservableDictionary<TKey, TValue> dictionary = new(equalityComparer);
    int nullKeys;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<KeyValuePair<TKey, TValue>>? select;
    internal readonly IEqualityComparer<TKey> EqualityComparer = equalityComparer;
    internal readonly Expression<Func<TElement, TKey>> KeySelector = keySelector;
    internal readonly Expression<Func<TElement, TValue>> ValueSelector = valueSelector;

    public override TValue this[TKey key]
    {
        get
        {
            lock (access)
                return dictionary[key];
        }
    }

    internal override IEqualityComparer<TKey> KeyComparer =>
        EqualityComparer;

    public override int Count
    {
        get
        {
            lock (access)
                return dictionary.Count;
        }
    }

    public override IEnumerable<TKey> Keys
    {
        get
        {
            lock (access)
                return dictionary.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock(access)
                return dictionary.Values.ToList().AsReadOnly();
        }
    }

    void BuildClaimsWithAccess()
    {
        claims.Clear();
        claimantsByKey.Clear();
        nullKeys = 0;
        var index = 0;
        foreach (var keyValuePair in select!)
        {
            var node = claims.Insert(index++, keyValuePair, 1);
            var key = keyValuePair.Key;
            if (key is null)
                ++nullKeys;
            else
                ClaimWithAccess(key, node);
        }
    }

    void ClaimWithAccess(TKey key, PrefixWeightedSequenceNode<KeyValuePair<TKey, TValue>> node)
    {
        if (claimantsByKey.TryGetValue(key, out var claimants))
            claimants.Add(node);
        else
            claimantsByKey.Add(key, [node]);
    }

    public override bool Contains(KeyValuePair<TKey, TValue> item)
    {
        lock (access)
            return dictionary.Contains(item);
    }

    public override bool ContainsKey(TKey key)
    {
        lock (access)
            return dictionary.ContainsKey(key);
    }

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (access)
            ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);
    }

    void DictionaryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void DictionaryDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

    void DictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void DictionaryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                select!.CollectionChanged -= SelectCollectionChanged;
                select.PropertyChanged -= SelectPropertyChanged;
                select.Dispose();
                dictionary.CollectionChanged -= DictionaryCollectionChanged;
                ((INotifyDictionaryChanged)dictionary).DictionaryChanged -= DictionaryDictionaryChangedBoxed;
                dictionary.DictionaryChanged -= DictionaryDictionaryChanged;
                dictionary.PropertyChanging -= DictionaryPropertyChanging;
                dictionary.PropertyChanged -= DictionaryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (access)
            return dictionary.ToList().AsReadOnly().GetEnumerator();
    }

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return dictionary.GetRange(keys);
    }

    protected override void OnInitialization()
    {
        var elementParameter = Expression.Parameter(typeof(TElement));
        select = source.ObserveSelect(Expression.Lambda<Func<TElement, KeyValuePair<TKey, TValue>>>(Expression.New(typeof(KeyValuePair<TKey, TValue>).GetConstructor([typeof(TKey), typeof(TValue)])!, Expression.Invoke(KeySelector, elementParameter), Expression.Invoke(ValueSelector, elementParameter)), elementParameter));
        lock (access)
        {
            BuildClaimsWithAccess();
            foreach (var (key, claimants) in claimantsByKey)
                dictionary.Add(key, WinningValueWithAccess(claimants));
            SetOperationFault();
        }
        dictionary.CollectionChanged += DictionaryCollectionChanged;
        ((INotifyDictionaryChanged)dictionary).DictionaryChanged += DictionaryDictionaryChangedBoxed;
        dictionary.DictionaryChanged += DictionaryDictionaryChanged;
        dictionary.PropertyChanging += DictionaryPropertyChanging;
        dictionary.PropertyChanged += DictionaryPropertyChanged;
        select.CollectionChanged += SelectCollectionChanged;
        select.PropertyChanged += SelectPropertyChanged;
    }

    void ReconcileWithAccess(IEnumerable<TKey> keys)
    {
        foreach (var key in keys)
            if (claimantsByKey.TryGetValue(key, out var claimants))
                dictionary[key] = WinningValueWithAccess(claimants);
            else
                dictionary.Remove(key);
    }

    void RelinquishWithAccess(TKey key, PrefixWeightedSequenceNode<KeyValuePair<TKey, TValue>> node)
    {
        if (!claimantsByKey.TryGetValue(key, out var claimants))
            return;
        claimants.Remove(node);
        if (claimants.Count == 0)
            claimantsByKey.Remove(key);
    }

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                BuildClaimsWithAccess();
                var replacement = new ObservableDictionary<TKey, TValue>(EqualityComparer);
                foreach (var (key, claimants) in claimantsByKey)
                    replacement.Add(key, WinningValueWithAccess(claimants));
                dictionary.Reset(replacement);
            }
            else if (e.Action is NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } movedItems && movedItems.Count > 0 && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex != e.NewStartingIndex)
                {
                    claims.MoveRange(e.OldStartingIndex, e.NewStartingIndex, movedItems.Count);
                    var contested = new List<TKey>();
                    foreach (var (key, claimants) in claimantsByKey)
                        if (claimants.Count > 1)
                            contested.Add(key);
                    ReconcileWithAccess(contested);
                }
            }
            else
            {
                var touched = new HashSet<TKey>(EqualityComparer);
                if (e.OldItems is { } oldItems && oldItems.Count > 0 && e.OldStartingIndex >= 0)
                    for (var i = oldItems.Count - 1; i >= 0; --i)
                    {
                        var node = claims.RemoveAt(e.OldStartingIndex + i);
                        var key = node.Item.Key;
                        if (key is null)
                            --nullKeys;
                        else
                        {
                            RelinquishWithAccess(key, node);
                            touched.Add(key);
                        }
                    }
                if (e.NewItems is { } newItems && newItems.Count > 0 && e.NewStartingIndex >= 0)
                    for (var i = 0; i < newItems.Count; ++i)
                    {
                        var keyValuePair = (KeyValuePair<TKey, TValue>)newItems[i]!;
                        var node = claims.Insert(e.NewStartingIndex + i, keyValuePair, 1);
                        var key = keyValuePair.Key;
                        if (key is null)
                            ++nullKeys;
                        else
                        {
                            ClaimWithAccess(key, node);
                            touched.Add(key);
                        }
                    }
                ReconcileWithAccess(touched);
            }
            SetOperationFault();
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
    }

    void SetOperationFault()
    {
        var faultList = new FaultList();
        if (nullKeys > 0)
            faultList.AddRange(Enumerable.Range(0, nullKeys).Select(_ => ExceptionHelper.KeyNull));
        var duplicateClaims = claims.Count - nullKeys - claimantsByKey.Count;
        if (duplicateClaims > 0)
            faultList.AddRange(Enumerable.Range(0, duplicateClaims).Select(_ => ExceptionHelper.SameKeyAlreadyAdded));
        faultList.Check(select!);
        OperationFault = faultList.Fault;
    }

    public override string ToString() =>
        $"conversion of {source} to a dictionary mapping keys using {KeySelector} and values using {ValueSelector}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return dictionary.TryGetValue(key, out value);
    }

    TValue WinningValueWithAccess(List<PrefixWeightedSequenceNode<KeyValuePair<TKey, TValue>>> claimants)
    {
        var winner = claimants[0];
        if (claimants.Count > 1)
        {
            var winningIndex = claims.IndexOf(winner);
            for (int i = 1, ii = claimants.Count; i < ii; ++i)
            {
                var index = claims.IndexOf(claimants[i]);
                if (index < winningIndex)
                {
                    winner = claimants[i];
                    winningIndex = index;
                }
            }
        }
        return winner.Item.Value;
    }
}
