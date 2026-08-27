namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionOrderByQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, IComparable>>, Expression<Func<TElement, Tuple<TElement, IComparable>>>> cachedWrappedSelectors = new(ExpressionEqualityComparer.Default);

    static Expression<Func<TElement, Tuple<TElement, IComparable>>> CachedWrappedSelectorsValueFactory(Expression<Func<TElement, IComparable>> selector)
    {
        var parameter = Expression.Parameter(typeof(TElement), "element");
        return Expression.Lambda<Func<TElement, Tuple<TElement, IComparable>>>(Expression.New(typeof(Tuple<TElement, IComparable>).GetConstructor([typeof(TElement), typeof(IComparable)])!, parameter, Expression.Invoke(selector, parameter)), parameter);
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
    readonly NullableKeyDictionary<TElement, PrefixWeightedSequenceNode<TElement>> nodesByElement;
    readonly PrefixWeightedSequence<TElement> positions;
    readonly ObservableRangeCollection<TElement> results;
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

    TElement ElementAtExcludingWithAccess(int index, int excludedIndex) =>
        positions.NodeAt(index < excludedIndex ? index : index + 1).Item;

    int FindDestinationWithAccess(TElement element, int currentIndex)
    {
        if (currentIndex > 0 && comparer!.Compare(element, ElementAtExcludingWithAccess(currentIndex - 1, currentIndex)) < 0)
        {
            var low = 0;
            var high = currentIndex - 1;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer!.Compare(element, ElementAtExcludingWithAccess(middle, currentIndex)) < 0)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }
        var reducedCount = positions.Count - 1;
        if (currentIndex < reducedCount && comparer!.Compare(element, ElementAtExcludingWithAccess(currentIndex, currentIndex)) > 0)
        {
            var low = currentIndex;
            var high = reducedCount;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (comparer!.Compare(element, ElementAtExcludingWithAccess(middle, currentIndex)) <= 0)
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
        var low = 0;
        var high = positions.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (comparer!.Compare(element, positions.NodeAt(middle).Item) < 0)
                high = middle;
            else
                low = middle + 1;
        }
        return low;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
            return results.ToList().AsReadOnly().GetEnumerator();
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
        var destinationIndex = FindDestinationWithAccess(element, currentIndex);
        if (destinationIndex == currentIndex)
            return;
        var startingIndex = positions.PrefixWeightBefore(currentIndex);
        positions.MoveRange(currentIndex, destinationIndex, 1);
        results.MoveRange(startingIndex, positions.PrefixWeightBefore(positions.IndexOf(node)), node.Weight);
    }

    void ResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ResultsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
            if (e.NewItems is { } newItems && newItems.Count > 0)
                foreach (var element in newItems.OfType<Tuple<TElement, IComparable>>().Select(t => t.Item1))
                    RepositionElementWithAccess(element);
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
                    else
                        foreach (var elements in oldItems.Cast<TElement>().GroupBy(element => element))
                        {
                            var element = elements.Key;
                            if (!nodesByElement.TryGetValue(element, out var node))
                                continue;
                            var removedCount = elements.Count();
                            results.RemoveRange(positions.PrefixWeightBefore(positions.IndexOf(node)), removedCount);
                            if (removedCount < node.Weight)
                                positions.SetWeight(node, node.Weight - removedCount);
                            else
                            {
                                positions.RemoveAt(positions.IndexOf(node));
                                nodesByElement.Remove(element);
                            }
                        }
                }
                if (e.NewItems is { } newItems && newItems.Count > 0)
                {
                    if (results.Count == 0)
                    {
                        var ordered = new List<TElement>();
                        RebuildPositionsWithAccess(newItems.Cast<TElement>().OrderBy(element => element, comparer).ToList(), ordered);
                        results.Reset(ordered);
                    }
                    else
                        foreach (var elements in newItems.Cast<TElement>().GroupBy(element => element))
                        {
                            var element = elements.Key;
                            var count = elements.Count();
                            if (nodesByElement.TryGetValue(element, out var node))
                            {
                                results.InsertRange(positions.PrefixWeightBefore(positions.IndexOf(node)), elements);
                                positions.SetWeight(node, node.Weight + count);
                            }
                            else
                            {
                                var index = FindInsertionIndexWithAccess(element);
                                nodesByElement.Add(element, positions.Insert(index, element, count));
                                results.InsertRange(positions.PrefixWeightBefore(index), elements);
                            }
                        }
                }
            }
        }
    }

    public override string ToString() =>
        $"ordering of {source} by {string.Join(" then ", SelectorsAndDirections.Select(selectorAndDirection => $"{selectorAndDirection.keySelectorExpression}{(selectorAndDirection.isDescending ? " descending" : string.Empty)}"))}";
}
