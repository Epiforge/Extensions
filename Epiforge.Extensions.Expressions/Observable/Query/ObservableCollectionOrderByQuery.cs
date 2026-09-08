namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionOrderByQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, IComparable>>, Expression<Func<TElement, Tuple<TElement, IComparable>>>> cachedWrappedSelectors = new(ExpressionEqualityComparer.Default);

    static Expression<Func<TElement, Tuple<TElement, IComparable>>> CachedWrappedSelectorsValueFactory(Expression<Func<TElement, IComparable>> selector)
    {
        var parameter = Expression.Parameter(typeof(TElement), "element");
        return Expression.Lambda<Func<TElement, Tuple<TElement, IComparable>>>(Expression.New(typeof(Tuple<TElement, IComparable>).GetConstructor([typeof(TElement), typeof(IComparable)])!, parameter, LambdaInvocationRewriter.Apply(selector, parameter) ?? Expression.Invoke(selector, parameter)), parameter);
    }

    public ObservableCollectionOrderByQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> selectorsAndDirections) :
        base(collectionObserver)
    {
        access = new();
        this.source = source;
        results = Logger is null ? new() : new(Logger);
        nodesByElement = [];
        positions = new();
        SelectorsAndDirections = selectorsAndDirections;
    }

    readonly object access;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableCollectionOrderingComparer<TElement>? comparer;
    List<TElement>? enumerationSnapshot;
    readonly NullableKeyDictionary<TElement, PrefixWeightedSequenceNode<TElement>> nodesByElement;
    readonly PrefixWeightedSequence<TElement> positions;
    readonly ObservableRangeCollection<TElement> results;
    readonly List<TElement> singleOccurrence = [default!];
    IReadOnlyList<(IObservableCollectionQuery<Tuple<TElement, IComparable>> selection, bool isDescending)>? selectionsAndDirections;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> SelectorsAndDirections;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
                return results[index];
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

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
                    foreach (var (selection, isDescending) in selectionsAndDirections!)
                        selection.CollectionChanged -= SelectionCollectionChanged;
                    source.CollectionChanged -= SourceCollectionChanged;
                    ((INotifyPropertyChanged)results).PropertyChanged -= ResultsPropertyChanged;
                    results.CollectionChanged -= ResultsCollectionChanged;
                    comparer!.Dispose();
                    foreach (var (selection, _) in selectionsAndDirections)
                        selection.Dispose();
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
    TElement ElementAtExcludingWithAccess(int index, int excludedIndex, ref PrefixWeightedSequenceNode<TElement> finger, ref int fingerIndex)
    {
        var positionIndex = index < excludedIndex ? index : index + 1;
        var node = positions.NodeAtFrom(finger, fingerIndex, positionIndex);
        finger = node;
        fingerIndex = positionIndex;
        return node.Item;
    }

    int FindDestinationWithAccess(TElement element, PrefixWeightedSequenceNode<TElement> node, int currentIndex)
    {
        var elementComparables = comparer!.ComparablesOf(element);
        var finger = node;
        var fingerIndex = currentIndex;
        if (currentIndex > 0 && comparer!.CompareWithComparablesOf(element, ref elementComparables, ElementAtExcludingWithAccess(currentIndex - 1, currentIndex, ref finger, ref fingerIndex)) < 0)
        {
            var low = 0;
            var high = currentIndex - 1;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer!.CompareWithComparablesOf(element, ref elementComparables, ElementAtExcludingWithAccess(middle, currentIndex, ref finger, ref fingerIndex)) < 0)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }
        var reducedCount = positions.Count - 1;
        if (currentIndex < reducedCount && comparer!.CompareWithComparablesOf(element, ref elementComparables, ElementAtExcludingWithAccess(currentIndex, currentIndex, ref finger, ref fingerIndex)) > 0)
        {
            var low = currentIndex;
            var high = reducedCount;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer!.CompareWithComparablesOf(element, ref elementComparables, ElementAtExcludingWithAccess(middle, currentIndex, ref finger, ref fingerIndex)) <= 0)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }
        return currentIndex;
    }

    int FindInsertionIndexWithAccess(TElement element)
    {
        var elementComparables = comparer!.ComparablesOf(element);
        var low = 0;
        var high = positions.Count;
        PrefixWeightedSequenceNode<TElement>? finger = null;
        var fingerIndex = 0;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var node = finger is null ? positions.NodeAt(middle) : positions.NodeAtFrom(finger, fingerIndex, middle);
            finger = node;
            fingerIndex = middle;
            if (comparer!.CompareWithComparablesOf(element, ref elementComparables, node.Item) < 0)
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

    protected override void OnInitialization()
    {
        lock (access)
        {
            selectionsAndDirections = SelectorsAndDirections.Select(t => (selection: source.ObserveSelect(cachedWrappedSelectors.GetOrAdd(t.keySelectorExpression, CachedWrappedSelectorsValueFactory)), t.isDescending)).ToList().AsReadOnly();
            comparer = new(access, selectionsAndDirections);
            var ordered = new List<TElement>();
            RebuildPositionsWithAccess(source.OrderBy(element => element, comparer).ToList(), ordered);
            results.Reset(ordered);
            results.CollectionChanged += ResultsCollectionChanged;
            ((INotifyPropertyChanged)results).PropertyChanged += ResultsPropertyChanged;
            source.CollectionChanged += SourceCollectionChanged;
            foreach (var (selection, isDescending) in selectionsAndDirections)
            {
                selection.CollectionChanged += SelectionCollectionChanged;
                selection.PropertyChanged += SelectionPropertyChanged;
            }
            SetOperationFault();
        }
    }

    void RebuildPositionsWithAccess(IReadOnlyList<TElement> fromSort, List<TElement> intoOrder)
    {
        positions.Clear();
        nodesByElement.Clear();
        for (int i = 0, ii = fromSort.Count; i < ii; ++i)
        {
            var element = fromSort[i];
            if (nodesByElement.TryGetValue(element, out var node))
                positions.SetWeight(node, node.Weight + 1);
            else
                nodesByElement.Add(element, positions.Insert(positions.Count, element, 1));
        }
        nodesByElement.TrimExcess();
        intoOrder.Clear();
        for (var node = positions.FirstNode; node is not null; node = positions.Next(node))
            for (int i = 0, ii = node.Weight; i < ii; ++i)
                intoOrder.Add(node.Item);
    }

    void RepositionElementWithAccess(TElement element)
    {
        if (!nodesByElement.TryGetValue(element, out var node))
            return;
        var currentIndex = positions.IndexOf(node);
        var destinationIndex = FindDestinationWithAccess(element, node, currentIndex);
        if (destinationIndex == currentIndex)
            return;
        var startingIndex = positions.PrefixWeightBefore(currentIndex);
        positions.MoveRange(currentIndex, destinationIndex, 1);
        results.MoveRange(startingIndex, positions.PrefixWeightBefore(node), node.Weight);
    }

    /// <summary>
    /// Places the specified number of occurrences of an element, which finds it a place in the order where it has none yet
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
        if (nodesByElement.TryGetValue(element, out var node))
        {
            results.InsertRange(positions.PrefixWeightBefore(node), occurrences);
            positions.SetWeight(node, node.Weight + count);
        }
        else
        {
            var index = FindInsertionIndexWithAccess(element);
            nodesByElement.Add(element, positions.Insert(index, element, count));
            results.InsertRange(positions.PrefixWeightBefore(index), occurrences);
        }
        singleOccurrence[0] = default!;
    }

    /// <summary>
    /// Forgets the specified number of occurrences of an element, which takes it out of the order where none remain
    /// </summary>
    void RemoveElementOccurrencesWithAccess(TElement element, int removedCount)
    {
        if (!nodesByElement.TryGetValue(element, out var node))
            return;
        results.RemoveRange(positions.PrefixWeightBefore(node), removedCount);
        if (removedCount < node.Weight)
            positions.SetWeight(node, node.Weight - removedCount);
        else
        {
            positions.RemoveAt(positions.IndexOf(node));
            nodesByElement.Remove(element);
        }
    }

    void ResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        enumerationSnapshot = null;
        OnCollectionChanged(e);
    }

    void ResultsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    /// <remarks>
    /// The payload is walked by index rather than through a query over it, because a key change carries a single item and the query allocates two iterators and their enumerators to deliver it
    /// </remarks>
    void SelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
            if (e.NewItems is { } newItems)
                for (int i = 0, ii = newItems.Count; i < ii; ++i)
                    if (newItems[i] is Tuple<TElement, IComparable> keyedElement)
                        RepositionElementWithAccess(keyedElement.Item1);
    }

    void SelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
    }

    void SetOperationFault()
    {
        lock (access)
        {
            var faultList = new FaultList();
            foreach (var (selection, _) in selectionsAndDirections!)
                faultList.Check(selection);
            OperationFault = faultList.Fault;
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                var ordered = new List<TElement>();
                RebuildPositionsWithAccess(source.OrderBy(element => element, comparer).ToList(), ordered);
                results.Reset(ordered);
                SetOperationFault();
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } oldItems && oldItems.Count > 0)
                {
                    if (oldItems.Count == results.Count)
                    {
                        positions.Clear();
                        nodesByElement.Clear();
                        nodesByElement.TrimExcess();
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
                        var ordered = new List<TElement>();
                        RebuildPositionsWithAccess(newItems.Cast<TElement>().OrderBy(element => element, comparer).ToList(), ordered);
                        results.Reset(ordered);
                    }
                    else if (newItems.Count == 1)
                        InsertElementOccurrencesWithAccess((TElement)newItems[0]!, 1, null);
                    else
                        foreach (var elements in newItems.Cast<TElement>().GroupBy(element => element))
                            InsertElementOccurrencesWithAccess(elements.Key, elements.Count(), elements);
                }
            }
        }
    }

    public override string ToString() =>
        $"ordering of {source} by {string.Join(" then ", SelectorsAndDirections.Select(selectorAndDirection => $"{selectorAndDirection.keySelectorExpression}{(selectorAndDirection.isDescending ? " descending" : string.Empty)}"))}";
}
