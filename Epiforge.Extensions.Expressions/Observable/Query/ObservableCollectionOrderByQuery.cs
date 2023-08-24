namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionOrderByQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    static readonly ConcurrentDictionary<Expression<Func<TElement, IComparable>>, Expression<Func<TElement, Tuple<TElement, IComparable>>>> cachedWrappedSelectors = new(ExpressionEqualityComparer.Default);

    static Expression<Func<TElement, Tuple<TElement, IComparable>>> CachedWrappedSelectorsValueFactory(Expression<Func<TElement, IComparable>> selector)
    {
        var parameter = Expression.Parameter(typeof(TElement), "element");
        return Expression.Lambda<Func<TElement, Tuple<TElement, IComparable>>>(Expression.New(typeof(Tuple<TElement, IComparable>).GetConstructor(new[] { typeof(TElement), typeof(IComparable) })!, parameter, Expression.Invoke(selector, parameter)), parameter);
    }

    public ObservableCollectionOrderByQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> selectorsAndDirections) :
        base(collectionObserver)
    {
        access = new();
        equalityComparer = EqualityComparer<TElement>.Default;
        this.source = source;
        results = Logger is null ? new() : new(Logger);
        startingIndiciesAndCounts = new();
        SelectorsAndDirections = selectorsAndDirections;
    }

    readonly object access;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableCollectionOrderingComparer<TElement>? comparer;
    readonly IEqualityComparer<TElement> equalityComparer;
    readonly ObservableRangeCollection<TElement> results;
    IReadOnlyList<(IObservableCollectionQuery<Tuple<TElement, IComparable>> selection, bool isDescending)>? selectionsAndDirections;
    readonly ObservableCollectionQuery<TElement> source;
    readonly NullableKeyDictionary<TElement, (int startingIndex, int count)> startingIndiciesAndCounts;

    internal readonly IReadOnlyList<(Expression<Func<TElement, IComparable>> keySelectorExpression, bool isDescending)> SelectorsAndDirections;

    public override TElement this[int index]
    {
        get
        {
            lock(access)
                return results[index];
        }
    }

    public override int Count
    {
        get
        {
            lock(access)
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

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock(access)
            foreach (var element in results)
                yield return element;
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            selectionsAndDirections = SelectorsAndDirections.Select(t => (selection: source.ObserveSelect(cachedWrappedSelectors.GetOrAdd(t.keySelectorExpression, CachedWrappedSelectorsValueFactory)), t.isDescending)).ToList().AsReadOnly();
            comparer = new(selectionsAndDirections);
            var sortedSource = source.ToList();
            sortedSource.Sort(comparer);
            RebuildStartingIndiciesAndCounts(sortedSource);
            results.Reset(sortedSource);
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

    void RebuildStartingIndiciesAndCounts(IReadOnlyList<TElement> fromSort)
    {
        startingIndiciesAndCounts.Clear();
        for (var i = 0; i < fromSort.Count; ++i)
        {
            var element = fromSort[i];
            if (startingIndiciesAndCounts.TryGetValue(element, out var startingIndexAndCount))
                startingIndiciesAndCounts[element] = (startingIndexAndCount.startingIndex, startingIndexAndCount.count + 1);
            else
                startingIndiciesAndCounts.Add(element, (i, 1));
        }
        startingIndiciesAndCounts.TrimExcess();
    }

    void RepositionElement(TElement element)
    {
        int startingIndex, count;
        if (startingIndiciesAndCounts.TryGetValue(element, out var startingIndexAndCount))
            (startingIndex, count) = startingIndexAndCount;
        else
            return;
        var index = startingIndex;

        bool performMove()
        {
            if (startingIndex != index)
            {
                startingIndiciesAndCounts[element] = (index, count);
                results.MoveRange(startingIndex, index, count);
                return true;
            }
            return false;
        }

        while (index > 0)
        {
            var otherElement = results[index - 1];
            if (comparer!.Compare(element, otherElement) >= 0)
                break;
            var (otherStartingIndex, otherCurrentCount) = startingIndiciesAndCounts[otherElement];
            startingIndiciesAndCounts[otherElement] = (otherStartingIndex + count, otherCurrentCount);
            index -= otherCurrentCount;
        }
        if (!performMove())
        {
            while (index < results.Count - count)
            {
                var otherElement = results[index + count];
                if (comparer!.Compare(element, otherElement) <= 0)
                    break;
                var (otherStartingIndex, otherCurrentCount) = startingIndiciesAndCounts[otherElement];
                startingIndiciesAndCounts[otherElement] = (otherStartingIndex - count, otherCurrentCount);
                index += otherCurrentCount;
            }
            performMove();
        }
    }

    void ResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ResultsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
            if (e.NewItems is { } newItems && newItems.Count > 0)
                foreach (var element in newItems.OfType<Tuple<TElement, IComparable>>().Select(t => t.Item1))
                    RepositionElement(element);
    }

    void SelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<Tuple<TElement, IComparable>>.OperationFault))
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
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                var sortedSource = source.ToList();
                sortedSource.Sort(comparer);
                RebuildStartingIndiciesAndCounts(sortedSource);
                results.Reset(sortedSource);
                SetOperationFault();
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                if (e.OldItems is { } oldItems && oldItems.Count > 0)
                {
                    if (oldItems.Count == results.Count)
                    {
                        startingIndiciesAndCounts.Clear();
                        startingIndiciesAndCounts.TrimExcess();
                        results.Clear();
                    }
                    else
                        foreach (var elements in oldItems.Cast<TElement>().GroupBy(element => element))
                        {
                            var element = elements.Key;
                            var (startingIndex, currentCount) = startingIndiciesAndCounts[element];
                            var removedCount = elements.Count();
                            results.RemoveRange(startingIndex, removedCount);
                            if (removedCount == currentCount)
                                startingIndiciesAndCounts.Remove(element);
                            else
                                startingIndiciesAndCounts[element] = (startingIndex, currentCount - removedCount);
                            foreach (var otherElement in startingIndiciesAndCounts.Keys.ToList().AsReadOnly())
                            {
                                var (otherStartingIndex, otherCurrentCount) = startingIndiciesAndCounts[otherElement];
                                if (otherStartingIndex > startingIndex)
                                    startingIndiciesAndCounts[otherElement] = (otherStartingIndex - removedCount, otherCurrentCount);
                            }
                        }
                }
                if (e.NewItems is { } newItems && newItems.Count > 0)
                {
                    if (results.Count == 0)
                    {
                        var sorted = e.NewItems.Cast<TElement>().ToList();
                        sorted.Sort(comparer);
                        RebuildStartingIndiciesAndCounts(sorted);
                        results.Reset(sorted);
                    }
                    else
                        foreach (var elements in newItems.Cast<TElement>().GroupBy(element => element))
                        {
                            var element = elements.Key;
                            var count = elements.Count();
                            var index = 0;
                            while (index < results.Count && comparer!.Compare(element, results[index]) >= 0)
                                ++index;
                            foreach (var startingIndexAndCountKv in startingIndiciesAndCounts.ToList().AsReadOnly())
                            {
                                var otherElement = startingIndexAndCountKv.Key;
                                if (!equalityComparer.Equals(otherElement, element))
                                {
                                    var (otherStartingIndex, otherCurrentCount) = startingIndexAndCountKv.Value;
                                    if (otherStartingIndex >= index)
                                        startingIndiciesAndCounts[otherElement] = (otherStartingIndex + count, otherCurrentCount);
                                }
                            }
                            results.InsertRange(index, elements);
                            if (startingIndiciesAndCounts.TryGetValue(element, out var startingIndexAndCount))
                                startingIndiciesAndCounts[element] = (startingIndexAndCount.startingIndex, startingIndexAndCount.count + count);
                            else
                                startingIndiciesAndCounts.Add(element, (index, count));
                        }
                }
            }
        }
    }

    public override string ToString() =>
        $"ordering of {source} by {string.Join(" then ", SelectorsAndDirections.Select(selectorAndDirection => $"{selectorAndDirection.keySelectorExpression}{(selectorAndDirection.isDescending ? " descending" : string.Empty)}"))}";
}
