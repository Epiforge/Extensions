namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Maps the elements of a source to a dictionary by a key and a value it observes for each distinct element directly, where the element earliest in the source wins a key more than one claims, and an element whose key or value faults claims no key
/// </summary>
sealed class ObservableCollectionToDictionaryQuery<TElement, TKey, TValue>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver),
    IObservableQueryDependent
    where TKey : notnull
{
    /// <summary>
    /// Pairs an element's key with its value, comparing both with their types' default equality, where a <see cref="KeyValuePair{TKey, TValue}" /> is compared as a value type is by default, boxing it
    /// </summary>
    internal readonly struct Claim(TKey key, TValue value) :
        IEquatable<Claim>
    {
        public TKey Key { get; } = key;

        public TValue Value { get; } = value;

        public bool Equals(Claim other) =>
            EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);

        public override bool Equals(object? obj) =>
            obj is Claim other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Key, Value);
    }

    /// <summary>
    /// What is kept for one distinct element: the observation of its key and value, their latest evaluation, and the positions at which the element occurs in the source
    /// </summary>
    sealed class Entry(TElement element, IObservableExpression<TElement, Claim> observation)
    {
        internal Claim Claim;
        internal readonly TElement Element = element;
        internal Exception? Fault;
        internal NodeSet<Entry> Nodes;
        internal readonly IObservableExpression<TElement, Claim> Observation = observation;
    }

    static readonly IEqualityComparer<TElement> elementComparer = typeof(TElement).IsValueType ? EqualityComparer<TElement>.Default : (IEqualityComparer<TElement>)(object)ReferenceEqualityComparer.Instance;
    static readonly ConditionalWeakTable<Expression<Func<TElement, TValue>>, ConditionalWeakTable<Expression<Func<TElement, TKey>>, Expression<Func<TElement, Claim>>>> pairSelectors = [];

    static Expression<Func<TElement, Claim>> AddPairSelector(ConditionalWeakTable<Expression<Func<TElement, TKey>>, Expression<Func<TElement, Claim>>> byKeySelector, Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector) =>
        byKeySelector.GetValue(keySelector, _ =>
        {
            var elementParameter = Expression.Parameter(typeof(TElement));
            return Expression.Lambda<Func<TElement, Claim>>(Expression.New(typeof(Claim).GetConstructor([typeof(TKey), typeof(TValue)])!, LambdaInvocationRewriter.Apply(keySelector, elementParameter) ?? Expression.Invoke(keySelector, elementParameter), LambdaInvocationRewriter.Apply(valueSelector, elementParameter) ?? Expression.Invoke(valueSelector, elementParameter)), elementParameter);
        });

    /// <summary>
    /// Yields the lambda pairing an element's key with its value, built once for each pair of selectors and kept for as long as both are, since the observer's caches of optimized and compiled lambdas match by reference
    /// </summary>
    /// <remarks>
    /// The table is keyed first by the value selector because the library's own identity selector is the value selector whenever a caller names only a key, and a table keyed first by a selector which dies while the second key lives keeps its pairings until the inner table has been finalized twice
    /// </remarks>
    static Expression<Func<TElement, Claim>> PairSelector(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector)
    {
        var byKeySelector = pairSelectors.GetValue(valueSelector, static _ => new());
        return byKeySelector.TryGetValue(keySelector, out var pairSelector) ? pairSelector : AddPairSelector(byKeySelector, keySelector, valueSelector);
    }

    readonly object access = new();
    readonly Dictionary<TKey, NodeSet<Entry>> claimantsByKey = new(equalityComparer);
    PropertyChangedEventHandler? claimEvaluationChangedHandler;
    int claimedOccurrences;
    readonly ObservableDictionary<TKey, TValue> dictionary = new(equalityComparer);
    readonly NullableKeyDictionary<TElement, Entry> entries = new(elementComparer);
    IReadOnlyList<KeyValuePair<TKey, TValue>>? enumerationSnapshot;
    readonly FaultList evaluationFaults = new();
    bool faultsChanged = true;
    IReadOnlyList<TKey>? keysSnapshot;
    int nullKeys;
    readonly PrefixWeightedSequence<Entry> occurrences = new();
    Expression<Func<TElement, Claim>>? pairSelector;
    bool released;
    int reportedDuplicateClaims;
    int reportedNullKeys;
    ObservableQuerySubscription? sourceSubscription;
    IReadOnlyList<TValue>? valuesSnapshot;
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
                return keysSnapshot ??= dictionary.Keys.ToList().AsReadOnly();
        }
    }

    public override IEnumerable<TValue> Values
    {
        get
        {
            lock(access)
                return valuesSnapshot ??= dictionary.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Yields the one handler this query attaches to every element it observes, since a method group converts to a new delegate at each conversion
    /// </summary>
    PropertyChangedEventHandler ClaimEvaluationChangedHandler =>
        claimEvaluationChangedHandler ??= ClaimEvaluationChanged;

    void AddOccurrenceWithAccess(TElement element, int index, HashSet<TKey>? touched)
    {
        if (!entries.TryGetValue(element, out var entry))
            entry = ObserveElementWithAccess(element);
        var node = occurrences.Insert(index, entry, 1);
        entry.Nodes.Add(node);
        ClaimWithAccess(entry, node, touched);
    }

    void BuildWithAccess()
    {
        var index = 0;
        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                AddOccurrenceWithAccess(source[i], index++, null);
        else
            foreach (var element in source)
                AddOccurrenceWithAccess(element, index++, null);
    }

    /// <summary>
    /// Records that an occurrence of an element claims the element's key, where the element's evaluation did not fault
    /// </summary>
    void ClaimWithAccess(Entry entry, PrefixWeightedSequenceNode<Entry> node, HashSet<TKey>? touched)
    {
        if (entry.Fault is not null)
            return;
        var key = entry.Claim.Key;
        if (key is null)
        {
            ++nullKeys;
            return;
        }
        ++claimedOccurrences;
        ref var claimants = ref CollectionsMarshal.GetValueRefOrNullRef(claimantsByKey, key);
        if (!Unsafe.IsNullRef(ref claimants))
            claimants.Add(node);
        else
            claimantsByKey.Add(key, new(node));
        touched?.Add(key);
    }

    void ClaimEvaluationChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, Claim> observation || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (released || !entries.TryGetValue(observation.Argument, out var entry) || !ReferenceEquals(entry.Observation, observation))
                return;
            var (fault, claim) = observation.Evaluation;
            var oldFault = entry.Fault;
            var oldClaim = entry.Claim;
            if (ReferenceEquals(oldFault, fault) && (fault is not null || oldClaim.Equals(claim)))
                return;
            if (!ReferenceEquals(oldFault, fault))
            {
                if (oldFault is not null)
                    evaluationFaults.RemoveElementOccurrence(entry.Element, elementComparer);
                if (fault is not null)
                    evaluationFaults.Add(new EvaluationFaultException(entry.Element, fault));
                faultsChanged = true;
            }
            if (oldFault is null && fault is null && (oldClaim.Key is null ? claim.Key is null : claim.Key is not null && EqualityComparer.Equals(oldClaim.Key, claim.Key)))
            {
                entry.Claim = claim;
                if (claim.Key is { } key && ReferenceEquals(WinningClaimantWithAccess(claimantsByKey[key]).Item, entry))
                    dictionary[key] = claim.Value;
            }
            else
            {
                var touched = new HashSet<TKey>(EqualityComparer);
                var nodes = entry.Nodes;
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    RelinquishWithAccess(entry, nodes[i], touched);
                entry.Fault = fault;
                entry.Claim = fault is null ? claim : default;
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    ClaimWithAccess(entry, nodes[i], touched);
                ReconcileWithAccess(touched);
            }
            SetOperationFaultWithAccess();
        }
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

    void DiscardSnapshots()
    {
        enumerationSnapshot = null;
        keysSnapshot = null;
        valuesSnapshot = null;
    }

    void DictionaryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DiscardSnapshots();
        OnCollectionChanged(e);
    }

    void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        DiscardSnapshots();
        OnDictionaryChanged(e);
    }

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
                lock (access)
                {
                    released = true;
                    if (sourceSubscription is not null)
                        source.UnsubscribeDependent(sourceSubscription);
                    ReleaseEntriesWithAccess();
                }
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
            return (enumerationSnapshot ??= dictionary.ToList().AsReadOnly()).GetEnumerator();
    }

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys)
    {
        lock (access)
            return dictionary.GetRange(keys);
    }

    /// <summary>
    /// Observes the claim of an element, subscribing before reading it and reading it only once the element is recorded, so that a change made on another thread in between is taken up once the lock is released rather than lost
    /// </summary>
    Entry ObserveElementWithAccess(TElement element)
    {
        var observation = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(pairSelector!, element);
        observation.PropertyChanged += ClaimEvaluationChangedHandler;
        var entry = new Entry(element, observation);
        entries.Add(element, entry);
        var (fault, claim) = observation.Evaluation;
        entry.Fault = fault;
        if (fault is null)
            entry.Claim = claim;
        else
        {
            evaluationFaults.Add(new EvaluationFaultException(element, fault));
            faultsChanged = true;
        }
        return entry;
    }

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            var oldCount = e.OldItems?.Count ?? 0;
            var newCount = e.NewItems?.Count ?? 0;
            if (e.Action is NotifyCollectionChangedAction.Reset || oldCount > 0 && (e.OldStartingIndex < 0 || e.OldStartingIndex + oldCount > occurrences.Count) || e.Action is not NotifyCollectionChangedAction.Move && newCount > 0 && e.NewStartingIndex < 0)
                RebuildWithAccess();
            else if (e.Action is NotifyCollectionChangedAction.Move)
            {
                if (oldCount > 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex != e.NewStartingIndex)
                {
                    occurrences.MoveRange(e.OldStartingIndex, e.NewStartingIndex, oldCount);
                    var contested = new HashSet<TKey>(EqualityComparer);
                    for (var i = 0; i < oldCount; ++i)
                        if (entries.TryGetValue((TElement)e.OldItems![i]!, out var entry) && entry.Fault is null && entry.Claim.Key is { } key && claimantsByKey.TryGetValue(key, out var claimants) && claimants.Count > 1)
                            contested.Add(key);
                    ReconcileWithAccess(contested);
                }
            }
            else
            {
                var touched = new HashSet<TKey>(EqualityComparer);
                for (var i = oldCount - 1; i >= 0; --i)
                    RemoveOccurrenceAtWithAccess(e.OldStartingIndex + i, touched);
                for (var i = 0; i < newCount; ++i)
                    AddOccurrenceWithAccess((TElement)e.NewItems![i]!, e.NewStartingIndex + i, touched);
                ReconcileWithAccess(touched);
            }
            SetOperationFaultWithAccess();
        }
    }

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                if (!released)
                    InheritOperationFault(source.OperationFault);
        }
    }

    protected override void OnInitialization()
    {
        pairSelector = PairSelector(KeySelector, ValueSelector);
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            BuildWithAccess();
            foreach (var (key, claimants) in claimantsByKey)
                dictionary.Add(key, WinningClaimantWithAccess(claimants).Item.Claim.Value);
            SetOperationFaultWithAccess();
            InheritOperationFault(source.OperationFault);
            dictionary.CollectionChanged += DictionaryCollectionChanged;
            ((INotifyDictionaryChanged)dictionary).DictionaryChanged += DictionaryDictionaryChangedBoxed;
            dictionary.DictionaryChanged += DictionaryDictionaryChanged;
            dictionary.PropertyChanging += DictionaryPropertyChanging;
            dictionary.PropertyChanged += DictionaryPropertyChanged;
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    void RebuildWithAccess()
    {
        ReleaseEntriesWithAccess();
        BuildWithAccess();
        var replacement = new ObservableDictionary<TKey, TValue>(EqualityComparer);
        foreach (var (key, claimants) in claimantsByKey)
            replacement.Add(key, WinningClaimantWithAccess(claimants).Item.Claim.Value);
        dictionary.Reset(replacement);
    }

    void ReconcileWithAccess(HashSet<TKey> keys)
    {
        foreach (var key in keys)
            if (claimantsByKey.TryGetValue(key, out var claimants))
                dictionary[key] = WinningClaimantWithAccess(claimants).Item.Claim.Value;
            else
                dictionary.Remove(key);
    }

    void ReleaseEntriesWithAccess()
    {
        foreach (var (_, entry) in entries)
        {
            entry.Observation.PropertyChanged -= ClaimEvaluationChangedHandler;
            entry.Observation.Dispose();
        }
        entries.Clear();
        occurrences.Clear();
        claimantsByKey.Clear();
        claimedOccurrences = 0;
        nullKeys = 0;
        evaluationFaults.Clear();
        faultsChanged = true;
    }

    /// <summary>
    /// Withdraws the claim an occurrence of an element made on the element's key, where it made one
    /// </summary>
    void RelinquishWithAccess(Entry entry, PrefixWeightedSequenceNode<Entry> node, HashSet<TKey> touched)
    {
        if (entry.Fault is not null)
            return;
        var key = entry.Claim.Key;
        if (key is null)
        {
            --nullKeys;
            return;
        }
        --claimedOccurrences;
        ref var claimants = ref CollectionsMarshal.GetValueRefOrNullRef(claimantsByKey, key);
        if (!Unsafe.IsNullRef(ref claimants))
        {
            claimants.Remove(node);
            if (claimants.Count == 0)
                claimantsByKey.Remove(key);
        }
        touched.Add(key);
    }

    void RemoveOccurrenceAtWithAccess(int index, HashSet<TKey> touched)
    {
        var node = occurrences.RemoveAt(index);
        var entry = node.Item;
        RelinquishWithAccess(entry, node, touched);
        entry.Nodes.Remove(node);
        if (entry.Nodes.Count > 0)
            return;
        entries.Remove(entry.Element);
        entry.Observation.PropertyChanged -= ClaimEvaluationChangedHandler;
        entry.Observation.Dispose();
        if (entry.Fault is not null)
        {
            evaluationFaults.RemoveElementOccurrence(entry.Element, elementComparer);
            faultsChanged = true;
        }
    }

    /// <summary>
    /// Sets the query's own fault from the faults of the evaluations and from the null and repeated keys claimed, where any of these has changed since it was last set
    /// </summary>
    void SetOperationFaultWithAccess()
    {
        var duplicateClaims = claimedOccurrences - claimantsByKey.Count;
        if (!faultsChanged && nullKeys == reportedNullKeys && duplicateClaims == reportedDuplicateClaims)
            return;
        faultsChanged = false;
        reportedNullKeys = nullKeys;
        reportedDuplicateClaims = duplicateClaims;
        var faultList = new FaultList();
        for (var i = 0; i < nullKeys; ++i)
            faultList.Add(ExceptionHelper.KeyNull);
        for (var i = 0; i < duplicateClaims; ++i)
            faultList.Add(ExceptionHelper.SameKeyAlreadyAdded);
        if (evaluationFaults.Fault is { } evaluationFault)
            faultList.Add(evaluationFault);
        OperationFault = faultList.Fault;
    }

    public override string ToString() =>
        $"conversion of {source} to a dictionary mapping keys using {KeySelector} and values using {ValueSelector}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        lock (access)
            return dictionary.TryGetValue(key, out value);
    }

    /// <summary>
    /// Finds which of the occurrences claiming a key stands earliest in the source, which is the one whose value the key maps to
    /// </summary>
    PrefixWeightedSequenceNode<Entry> WinningClaimantWithAccess(NodeSet<Entry> claimants)
    {
        var winner = claimants[0];
        if (claimants.Count > 1)
        {
            var winningIndex = occurrences.IndexOf(winner);
            for (int i = 1, ii = claimants.Count; i < ii; ++i)
            {
                var index = occurrences.IndexOf(claimants[i]);
                if (index < winningIndex)
                {
                    winner = claimants[i];
                    winningIndex = index;
                }
            }
        }
        return winner;
    }
}
