namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Orders the elements of a source by keys it observes for each distinct element directly, leaving an element out while any of its keys faults
/// </summary>
sealed class ObservableCollectionOrderByQuery<TElement> :
    ObservableCollectionQuery<TElement>,
    IObservableQueryDependent
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, IComparable>>, Expression<Func<TElement, IComparable>>> stableKeySelectors = new(ExpressionEqualityComparer.Default);

    /// <summary>
    /// Yields one instance for every key selector equal in structure where every constant it holds compares by value, so that a caller writing a selector which captures nothing where it is used still shares one compilation, since the observer's caches of optimized and compiled lambdas match by reference; a selector holding a closure is used as it is and never retained
    /// </summary>
    static Expression<Func<TElement, IComparable>> SharedKeySelector(Expression<Func<TElement, IComparable>> keySelector) =>
        ExpressionKeyStability.IsStable(keySelector) ? stableKeySelectors.GetOrAdd(keySelector, keySelector) : keySelector;

    public ObservableCollectionOrderByQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> selectorsAndDirections) :
        base(collectionObserver)
    {
        access = new();
        this.source = source;
        results = Logger is null ? new() : new(Logger);
        entries = [];
        positions = new();
        SelectorsAndDirections = selectorsAndDirections;
        keySelectors = [..selectorsAndDirections.Select(selectorAndDirection => SharedKeySelector(selectorAndDirection.keySelectorExpression))];
        comparer = new([..selectorsAndDirections.Select(selectorAndDirection => selectorAndDirection.isDescending)]);
    }

    const int farthestGallopingStep = 8;

    readonly object access;
    NullableKeyDictionary<TElement, ObservableCollectionRankQuery<TElement>>? cachedRankQueries;
    readonly ObservableCollectionOrderingComparer<TElement> comparer;
    List<TElement>? enumerationSnapshot;
    readonly NullableKeyDictionary<TElement, ObservableCollectionOrderingComparer<TElement>.Entry> entries;
    readonly FaultList faults = new();
    bool faultsChanged;
    PropertyChangedEventHandler? keyEvaluationChangedHandler;
    readonly Expression<Func<TElement, IComparable>>[] keySelectors;
    readonly PrefixWeightedSequence<ObservableCollectionOrderingComparer<TElement>.Entry> positions;
    bool released;
    readonly ObservableRangeCollection<TElement> results;
    readonly List<TElement> singleOccurrence = [default!];
    readonly ObservableCollectionQuery<TElement> source;
    ObservableQuerySubscription? sourceSubscription;
    int withheldOccurrences;

    internal readonly IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> SelectorsAndDirections;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
                return results[index];
        }
    }

    public override int CachedObservableQueries
    {
        get
        {
            var count = base.CachedObservableQueries;
            lock (access)
                count += cachedRankQueries?.Values.Sum(rankQuery => 1 + rankQuery.CachedObservableQueries) ?? 0;
            return count;
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return results.Count;
        }
    }

    /// <summary>
    /// Yields the one handler this query attaches to every key it observes, since a method group converts to a new delegate at each conversion
    /// </summary>
    PropertyChangedEventHandler KeyEvaluationChangedHandler =>
        keyEvaluationChangedHandler ??= KeyEvaluationChanged;

    /// <summary>
    /// Occurs when the key of an element in the ordering changes, whether or not the element moves
    /// </summary>
    internal event EventHandler? KeysChanged;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
                    released = true;
                    if (sourceSubscription is not null)
                        source.UnsubscribeDependent(sourceSubscription);
                    ((INotifyPropertyChanged)results).PropertyChanged -= ResultsPropertyChanged;
                    results.CollectionChanged -= ResultsCollectionChanged;
                    ReleaseEntriesWithAccess();
                    RemovedFromCache();
                }
            return removedFromCache;
        }
        return true;
    }

    /// <summary>
    /// Takes the element at a position in the order, counting as though the element being repositioned were not in it, and leaves the node it found as the finger for the next call
    /// </summary>
    /// <remarks>
    /// The search which uses this probes positions which converge on one another, so each node is found from the one before it rather than from the root of the sequence. <c>NodeAtFrom</c> decides for itself whether the finger is nearer than the root and descends from the root when it is not, so this cannot reach a different node than a descent would
    /// </remarks>
    ObservableCollectionOrderingComparer<TElement>.Entry EntryAtExcludingWithAccess(int index, int excludedIndex, ref PrefixWeightedSequenceNode<ObservableCollectionOrderingComparer<TElement>.Entry> finger, ref int fingerIndex)
    {
        var positionIndex = index < excludedIndex ? index : index + 1;
        var node = positions.NodeAtFrom(finger, fingerIndex, positionIndex);
        finger = node;
        fingerIndex = positionIndex;
        return node.Item;
    }

    /// <summary>
    /// Finds where an element whose key changed belongs, counting as though it were not in the order, by comparing it with its neighbors and then with elements one, two, four and eight places further on, before searching by halves whatever the last comparison left
    /// </summary>
    /// <remarks>
    /// A key change usually moves an element a few places, which this settles in a few comparisons, where a search by halves over everything on the side it moves to takes the logarithm of the order's size however near it lands; a move across the order costs at most four comparisons more than that search
    /// </remarks>
    int FindDestinationWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry, PrefixWeightedSequenceNode<ObservableCollectionOrderingComparer<TElement>.Entry> node, int currentIndex)
    {
        var finger = node;
        var fingerIndex = currentIndex;
        if (currentIndex > 0 && comparer.Compare(entry, EntryAtExcludingWithAccess(currentIndex - 1, currentIndex, ref finger, ref fingerIndex)) < 0)
        {
            var low = 0;
            var high = currentIndex - 1;
            for (var step = 1; step <= farthestGallopingStep; step <<= 1)
            {
                var probe = high - step;
                if (probe < 0)
                    break;
                if (comparer.Compare(entry, EntryAtExcludingWithAccess(probe, currentIndex, ref finger, ref fingerIndex)) >= 0)
                {
                    low = probe + 1;
                    break;
                }
                high = probe;
            }
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer.Compare(entry, EntryAtExcludingWithAccess(middle, currentIndex, ref finger, ref fingerIndex)) < 0)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }
        var reducedCount = positions.Count - 1;
        if (currentIndex < reducedCount && comparer.Compare(entry, EntryAtExcludingWithAccess(currentIndex, currentIndex, ref finger, ref fingerIndex)) > 0)
        {
            var low = currentIndex + 1;
            var high = reducedCount;
            for (var step = 1; step <= farthestGallopingStep; step <<= 1)
            {
                var probe = low - 1 + step;
                if (probe >= reducedCount)
                    break;
                if (comparer.Compare(entry, EntryAtExcludingWithAccess(probe, currentIndex, ref finger, ref fingerIndex)) <= 0)
                {
                    high = probe;
                    break;
                }
                low = probe + 1;
            }
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer.Compare(entry, EntryAtExcludingWithAccess(middle, currentIndex, ref finger, ref fingerIndex)) <= 0)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }
        return currentIndex;
    }

    int FindInsertionIndexWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry)
    {
        var low = 0;
        var high = positions.Count;
        PrefixWeightedSequenceNode<ObservableCollectionOrderingComparer<TElement>.Entry>? finger = null;
        var fingerIndex = 0;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var node = finger is null ? positions.NodeAt(middle) : positions.NodeAtFrom(finger, fingerIndex, middle);
            finger = node;
            fingerIndex = middle;
            if (comparer.Compare(entry, node.Item) < 0)
                high = middle;
            else
                low = middle + 1;
        }
        return low;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
        {
            enumerationSnapshot ??= results.ToList();
            return enumerationSnapshot.GetEnumerator();
        }
    }

    /// <summary>
    /// Places the specified number of occurrences of an element, which finds it a place in the order where it has none yet and holds them back where one of its keys faults
    /// </summary>
    /// <param name="element">The element to place</param>
    /// <param name="count">The number of occurrences of it to place</param>
    /// <param name="occurrences">The occurrences to place, which is <c>null</c> for a single one, in which case the buffer this keeps for the purpose stands in for them</param>
    /// <remarks>
    /// The buffer is safe to reuse because every caller holds the query's lock and <see cref="ObservableRangeCollection{T}.InsertRange(int, IEnumerable{T})" /> copies what it is given before it announces anything
    /// </remarks>
    void InsertElementOccurrencesWithAccess(TElement element, int count, IEnumerable<TElement>? occurrences)
    {
        if (occurrences is null)
        {
            singleOccurrence[0] = element;
            occurrences = singleOccurrence;
        }
        if (entries.TryGetValue(element, out var entry))
        {
            entry.Occurrences += count;
            if (entry.Node is { } node)
            {
                results.InsertRange(positions.PrefixWeightBefore(node), occurrences);
                positions.SetWeight(node, node.Weight + count);
            }
            else
                withheldOccurrences += count;
        }
        else
        {
            entry = ObserveElementWithAccess(element);
            entry.Occurrences = count;
            if (entry.FaultedKeys == 0)
                PlaceWithAccess(entry, occurrences);
            else
                withheldOccurrences += count;
        }
        singleOccurrence[0] = default!;
    }

    void KeyEvaluationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IObservableExpression<TElement, IComparable> observation || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        var keyChanged = false;
        using (var notificationDeferral = DeferNotificationsUntilMutationCompletes())
            lock (access)
                if (!released)
                {
                    keyChanged = KeyEvaluationChangedWithAccess(observation);
                    TakeFaultWithAccess();
                }
        if (keyChanged)
            KeysChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes up a new evaluation of one key of an element, which moves the element where it stays in the order, takes it out where the key began faulting and puts it back where the last of its keys stopped
    /// </summary>
    /// <returns><c>true</c> if the key changed; otherwise, <c>false</c></returns>
    bool KeyEvaluationChangedWithAccess(IObservableExpression<TElement, IComparable> observation)
    {
        if (!entries.TryGetValue(observation.Argument, out var entry))
            return false;
        var keyCount = keySelectors.Length;
        var keyIndex = 0;
        while (keyIndex < keyCount && !ReferenceEquals(entry.KeyAt(keyIndex).Observation, observation))
            ++keyIndex;
        if (keyIndex == keyCount)
            return false;
        ref var key = ref entry.KeyAt(keyIndex);
        var (fault, comparable) = observation.Evaluation;
        var oldFault = key.Fault;
        if (ReferenceEquals(oldFault, fault) && (fault is not null || Equals(key.Comparable, comparable)))
            return false;
        key.Fault = fault;
        key.Comparable = fault is null ? comparable : null;
        if (!ReferenceEquals(oldFault, fault))
        {
            entry.FaultedKeys += (fault is null ? 0 : 1) - (oldFault is null ? 0 : 1);
            RecordFaultsWithAccess(entry);
        }
        if (entry.Node is { } node)
        {
            if (entry.FaultedKeys > 0)
            {
                results.RemoveRange(positions.PrefixWeightBefore(node), node.Weight);
                positions.RemoveAt(positions.IndexOf(node));
                entry.Node = null;
                withheldOccurrences += entry.Occurrences;
            }
            else
                RepositionWithAccess(entry, node);
        }
        else if (entry.FaultedKeys == 0)
        {
            withheldOccurrences -= entry.Occurrences;
            PlaceWithAccess(entry, Enumerable.Repeat(entry.Element, entry.Occurrences));
        }
        return true;
    }

    /// <summary>
    /// Observes the keys of an element, subscribing to each before reading any and reading them only once the element is recorded, so that a change made on another thread in between is taken up once the lock is released rather than lost
    /// </summary>
    ObservableCollectionOrderingComparer<TElement>.Entry ObserveElementWithAccess(TElement element)
    {
        var keyCount = keySelectors.Length;
        var entry = ObservableCollectionOrderingComparer<TElement>.Entry.Create(element, keyCount);
        for (var i = 0; i < keyCount; ++i)
        {
            var observation = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(keySelectors[i], element);
            observation.PropertyChanged += KeyEvaluationChangedHandler;
            entry.KeyAt(i).Observation = observation;
        }
        entries.Add(element, entry);
        for (var i = 0; i < keyCount; ++i)
        {
            ref var key = ref entry.KeyAt(i);
            (key.Fault, key.Comparable) = key.Observation.Evaluation;
            if (key.Fault is { } fault)
            {
                key.Comparable = null;
                ++entry.FaultedKeys;
                faults.Add(new EvaluationFaultException(element, fault));
                faultsChanged = true;
            }
        }
        return entry;
    }

    /// <summary>
    /// Observes the keys of every element of the source, in the order the source first holds each, counting the occurrences of each
    /// </summary>
    List<ObservableCollectionOrderingComparer<TElement>.Entry> ObserveSourceWithAccess()
    {
        var firstOccurrences = new List<ObservableCollectionOrderingComparer<TElement>.Entry>();
        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                ObserveOccurrenceWithAccess(source[i], firstOccurrences);
        else
            foreach (var element in source)
                ObserveOccurrenceWithAccess(element, firstOccurrences);
        return firstOccurrences;
    }

    void ObserveOccurrenceWithAccess(TElement element, List<ObservableCollectionOrderingComparer<TElement>.Entry> firstOccurrences)
    {
        if (entries.TryGetValue(element, out var entry))
            ++entry.Occurrences;
        else
        {
            entry = ObserveElementWithAccess(element);
            entry.Occurrences = 1;
            firstOccurrences.Add(entry);
        }
    }

    [return: DisposeWhenDiscarded]
    internal IObservableScalarQuery<(int Ahead, int Tied)> ObserveRank(TElement element)
    {
        ObservableCollectionRankQuery<TElement> rankQuery;
        lock (access)
        {
            cachedRankQueries ??= [];
            if (!cachedRankQueries.TryGetValue(element, out rankQuery!))
            {
                rankQuery = new ObservableCollectionRankQuery<TElement>(collectionObserver, this, element);
                cachedRankQueries.Add(element, rankQuery);
            }
            ++rankQuery.Observations;
        }
        rankQuery.Initialize();
        return rankQuery.AsScoped();
    }

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                ReleaseEntriesWithAccess();
                var ordered = new List<TElement>();
                PlaceAllWithAccess(ObserveSourceWithAccess(), ordered);
                results.Reset(ordered);
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } oldItems && oldItems.Count > 0)
                {
                    if (oldItems.Count == results.Count + withheldOccurrences)
                    {
                        ReleaseEntriesWithAccess();
                        results.Clear();
                    }
                    else if (oldItems.Count == 1)
                        RemoveElementOccurrencesWithAccess((TElement)oldItems[0]!, 1);
                    else
                        foreach (var elements in oldItems.Cast<TElement>().GroupBy(element => element))
                            RemoveElementOccurrencesWithAccess(elements.Key, elements.Count());
                }
                if (e.NewItems is { } newItems && newItems.Count > 0)
                {
                    if (results.Count == 0)
                    {
                        var firstOccurrences = new List<ObservableCollectionOrderingComparer<TElement>.Entry>();
                        for (int i = 0, ii = newItems.Count; i < ii; ++i)
                            ObserveOccurrenceWithAccess((TElement)newItems[i]!, firstOccurrences);
                        var ordered = new List<TElement>();
                        PlaceAllWithAccess(firstOccurrences, ordered);
                        if (ordered.Count > 0)
                            results.Reset(ordered);
                    }
                    else if (newItems.Count == 1)
                        InsertElementOccurrencesWithAccess((TElement)newItems[0]!, 1, null);
                    else
                        foreach (var elements in newItems.Cast<TElement>().GroupBy(element => element))
                            InsertElementOccurrencesWithAccess(elements.Key, elements.Count(), elements);
                }
            }
            TakeFaultWithAccess();
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
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            var ordered = new List<TElement>();
            PlaceAllWithAccess(ObserveSourceWithAccess(), ordered);
            results.Reset(ordered);
            results.CollectionChanged += ResultsCollectionChanged;
            ((INotifyPropertyChanged)results).PropertyChanged += ResultsPropertyChanged;
            TakeFaultWithAccess();
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    /// <summary>
    /// Places elements newly observed into an order holding none, sorting those whose keys are all known and holding back those with a key which faults, and lists the whole order
    /// </summary>
    /// <remarks>
    /// The sort is stable, so elements whose keys tie stand in the order the source first holds them, as they would had they been placed one at a time. Occurrences held back are counted afresh, since elements held back before these arrived may have gained occurrences among them
    /// </remarks>
    void PlaceAllWithAccess(List<ObservableCollectionOrderingComparer<TElement>.Entry> firstOccurrences, List<TElement> intoOrder)
    {
        var placeable = new List<ObservableCollectionOrderingComparer<TElement>.Entry>(firstOccurrences.Count);
        foreach (var entry in firstOccurrences)
            if (entry.FaultedKeys == 0)
                placeable.Add(entry);
        foreach (var entry in placeable.OrderBy(entry => entry, comparer))
            entry.Node = positions.Insert(positions.Count, entry, entry.Occurrences);
        withheldOccurrences = 0;
        foreach (var (_, entry) in entries)
            if (entry.Node is null)
                withheldOccurrences += entry.Occurrences;
        intoOrder.Clear();
        for (var node = positions.FirstNode; node is not null; node = positions.Next(node))
            for (int i = 0, ii = node.Weight; i < ii; ++i)
                intoOrder.Add(node.Item.Element);
    }

    void PlaceWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry, IEnumerable<TElement> occurrences)
    {
        var index = FindInsertionIndexWithAccess(entry);
        entry.Node = positions.Insert(index, entry, entry.Occurrences);
        results.InsertRange(positions.PrefixWeightBefore(index), occurrences);
    }

    internal bool QueryDisposed(ObservableCollectionRankQuery<TElement> rankQuery)
    {
        lock (access)
        {
            var remaining = --rankQuery.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedRankQueries!.Remove(rankQuery.Element);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Counts the occurrences which sort strictly ahead of an element and those which sort level with it, the element's own included
    /// </summary>
    /// <remarks>
    /// Two searches over the distinct elements in order, the first for the earliest which does not sort ahead of the element and the second, starting there, for the earliest which sorts after it; the weights before each are the counts. Each search finds its nodes from the one before, as the search for an insertion point does
    /// </remarks>
    internal (Exception? Fault, (int Ahead, int Tied) Result) RankOf(TElement element)
    {
        lock (access)
        {
            if (OperationFault is { } fault)
                return (fault, default);
            if (!entries.TryGetValue(element, out var entry) || entry.Node is null)
                return (ExceptionHelper.SequenceContainsNoMatchingElement, default);
            PrefixWeightedSequenceNode<ObservableCollectionOrderingComparer<TElement>.Entry>? finger = null;
            var fingerIndex = 0;
            var low = 0;
            var high = positions.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var node = finger is null ? positions.NodeAt(middle) : positions.NodeAtFrom(finger, fingerIndex, middle);
                finger = node;
                fingerIndex = middle;
                if (comparer.Compare(entry, node.Item) > 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            var firstLevel = low;
            high = positions.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var node = finger is null ? positions.NodeAt(middle) : positions.NodeAtFrom(finger, fingerIndex, middle);
                finger = node;
                fingerIndex = middle;
                if (comparer.Compare(entry, node.Item) >= 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            var ahead = positions.PrefixWeightBefore(firstLevel);
            return (null, (ahead, positions.PrefixWeightBefore(low) - ahead));
        }
    }

    /// <summary>
    /// Records the faults of an element's keys afresh, which is how one of them changing is recorded
    /// </summary>
    void RecordFaultsWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry)
    {
        faults.RemoveKey(entry.Element, EqualityComparer<TElement>.Default);
        for (int i = 0, ii = keySelectors.Length; i < ii; ++i)
            if (entry.KeyAt(i).Fault is { } fault)
                faults.Add(new EvaluationFaultException(entry.Element, fault));
        faultsChanged = true;
    }

    void ReleaseEntriesWithAccess()
    {
        foreach (var entry in entries.Values)
            for (int i = 0, ii = keySelectors.Length; i < ii; ++i)
            {
                var observation = entry.KeyAt(i).Observation;
                observation.PropertyChanged -= KeyEvaluationChangedHandler;
                observation.Dispose();
            }
        entries.Clear();
        positions.Clear();
        withheldOccurrences = 0;
        faults.Clear();
        faultsChanged = true;
    }

    void ReleaseEntryWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry)
    {
        entries.Remove(entry.Element);
        for (int i = 0, ii = keySelectors.Length; i < ii; ++i)
        {
            var observation = entry.KeyAt(i).Observation;
            observation.PropertyChanged -= KeyEvaluationChangedHandler;
            observation.Dispose();
        }
        if (entry.FaultedKeys > 0)
        {
            faults.RemoveKey(entry.Element, EqualityComparer<TElement>.Default);
            faultsChanged = true;
        }
    }

    /// <summary>
    /// Forgets the specified number of occurrences of an element, which takes it out of the order and stops observing its keys where none remain
    /// </summary>
    void RemoveElementOccurrencesWithAccess(TElement element, int removedCount)
    {
        if (!entries.TryGetValue(element, out var entry))
            return;
        if (entry.Node is { } node)
        {
            results.RemoveRange(positions.PrefixWeightBefore(node), removedCount);
            if (removedCount < node.Weight)
                positions.SetWeight(node, node.Weight - removedCount);
            else
            {
                positions.RemoveAt(positions.IndexOf(node));
                entry.Node = null;
            }
        }
        else
            withheldOccurrences -= removedCount;
        entry.Occurrences -= removedCount;
        if (entry.Occurrences <= 0)
            ReleaseEntryWithAccess(entry);
    }

    void RepositionWithAccess(ObservableCollectionOrderingComparer<TElement>.Entry entry, PrefixWeightedSequenceNode<ObservableCollectionOrderingComparer<TElement>.Entry> node)
    {
        var currentIndex = positions.IndexOf(node);
        var destinationIndex = FindDestinationWithAccess(entry, node, currentIndex);
        if (destinationIndex == currentIndex)
            return;
        var startingIndex = positions.PrefixWeightBefore(currentIndex);
        positions.Move(node, destinationIndex);
        results.MoveRange(startingIndex, positions.PrefixWeightBefore(node), node.Weight);
    }

    void ResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        enumerationSnapshot = null;
        OnCollectionChanged(e);
    }

    void ResultsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void TakeFaultWithAccess()
    {
        if (!faultsChanged)
            return;
        faultsChanged = false;
        OperationFault = faults.Fault;
    }

    public override string ToString() =>
        $"ordering of {source} by {string.Join(" then ", SelectorsAndDirections.Select(selectorAndDirection => $"{selectorAndDirection.keySelectorExpression}{(selectorAndDirection.isDescending ? " descending" : string.Empty)}"))}";
}
