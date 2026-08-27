namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionOrderingComparer<TElement> :
    SyncDisposable,
    IComparer<TElement>
{
    public ObservableCollectionOrderingComparer(object access, IReadOnlyList<(IObservableCollectionQuery<Tuple<TElement, IComparable>> selection, bool isDescending)> selectionsAndDirections)
    {
        this.access = access;
        comparables = [];
        counts = [];
        this.selectionsAndDirections = selectionsAndDirections;
        lock (access)
        {
            var wiredSelections = new HashSet<IObservableCollectionQuery<Tuple<TElement, IComparable>>>();
            foreach (var (selection, _) in selectionsAndDirections)
                if (wiredSelections.Add(selection))
                    selection.CollectionChanged += SelectionCollectionChanged;
            if (selectionsAndDirections.Count > 0)
                lastSelectionAndDirection = selectionsAndDirections[^1];
            RebuildWithAccess();
        }
    }

    readonly object access;
    readonly NullableKeyDictionary<TElement, List<IComparable>> comparables;
    bool comparablesAreStale;
    readonly NullableKeyDictionary<TElement, int> counts;
    readonly (IObservableCollectionQuery<Tuple<TElement, IComparable>> selection, bool isDescending) lastSelectionAndDirection;
    readonly IReadOnlyList<(IObservableCollectionQuery<Tuple<TElement, IComparable>> selection, bool isDescending)> selectionsAndDirections;

    static void AssignComparable(List<IComparable> elementComparables, int selectionIndex, IComparable comparable)
    {
        if (elementComparables.Count == selectionIndex)
            elementComparables.Add(comparable);
        else if (selectionIndex < elementComparables.Count)
            elementComparables[selectionIndex] = comparable;
    }

    public int Compare(TElement? x, TElement? y)
    {
        if (comparablesAreStale)
            RebuildWithAccess();
        comparables.TryGetValue(x!, out var xList);
        comparables.TryGetValue(y!, out var yList);
        for (var i = 0; i < selectionsAndDirections.Count; ++i)
        {
            var isDescending = selectionsAndDirections[i].isDescending;
            var xComparable = xList is not null && i < xList.Count ? xList[i] : null;
            var yComparable = yList is not null && i < yList.Count ? yList[i] : null;
            if (xComparable is null)
                return yComparable is null ? 0 : isDescending ? 1 : -1;
            else if (yComparable is null)
                return isDescending ? -1 : 1;
            var comparison = xComparable.CompareTo(yComparable);
            if (comparison != 0)
                return comparison * (isDescending ? -1 : 1);
        }
        return 0;
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
            foreach (var (selection, _) in selectionsAndDirections)
                selection.CollectionChanged -= SelectionCollectionChanged;
        return true;
    }

    void RebuildWithAccess()
    {
        comparablesAreStale = false;
        comparables.Clear();
        counts.Clear();
        if (selectionsAndDirections.Count == 0)
            return;
        foreach (var elementComparables in selectionsAndDirections[0].selection.GroupBy(t => t.Item1, t => t.Item2))
        {
            List<IComparable> elementComparablesList = [elementComparables.First()];
            comparables.Add(elementComparables.Key, elementComparablesList);
            counts.Add(elementComparables.Key, elementComparables.Count());
        }
        for (var i = 1; i < selectionsAndDirections.Count; ++i)
            foreach (var elementComparables in selectionsAndDirections[i].selection.GroupBy(t => t.Item1, t => t.Item2))
                if (comparables.TryGetValue(elementComparables.Key, out var elementComparablesList))
                    AssignComparable(elementComparablesList, i, elementComparables.First());
        comparables.TrimExcess();
        counts.TrimExcess();
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    void SelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                comparablesAreStale = true;
                return;
            }
            if (comparablesAreStale || e.Action is NotifyCollectionChangedAction.Move)
                return;
            if (e.Action is NotifyCollectionChangedAction.Replace && (e.OldItems?.Count ?? 0) == 1 && (e.NewItems?.Count ?? 0) == 1 && e.OldItems![0] is Tuple<TElement, IComparable> oldItem && e.NewItems![0] is Tuple<TElement, IComparable> newItem && ReferenceEquals(oldItem.Item1, newItem.Item1))
            {
                if (comparables.TryGetValue(oldItem.Item1, out var replacedComparables))
                    foreach (var selectionIndex in selectionsAndDirections.FindIndicies(t => ReferenceEquals(t.selection, sender)))
                        AssignComparable(replacedComparables, selectionIndex, newItem.Item2);
                return;
            }
            if ((e.OldItems?.Count ?? 0) > 0 && ReferenceEquals(sender, lastSelectionAndDirection.selection))
                foreach (var elementComparables in e.OldItems!.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                {
                    var element = elementComparables.Key;
                    if (!counts.TryGetValue(element, out var currentCount))
                        continue;
                    var countDiff = currentCount - elementComparables.Count();
                    if (countDiff <= 0)
                    {
                        counts.Remove(element);
                        comparables.Remove(element);
                    }
                    else
                        counts[element] = countDiff;
                }
            if ((e.NewItems?.Count ?? 0) > 0)
                foreach (var selectionIndex in selectionsAndDirections.FindIndicies(t => ReferenceEquals(t.selection, sender)))
                    if (selectionIndex == 0)
                        foreach (var elementComparables in e.NewItems!.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                        {
                            var element = elementComparables.Key;
                            var count = elementComparables.Count();
                            if (!comparables.TryGetValue(element, out var elementComparablesList))
                            {
                                elementComparablesList = [];
                                comparables.Add(element, elementComparablesList);
                                counts.Add(element, count);
                            }
                            else
                                counts[element] += count;
                            AssignComparable(elementComparablesList, 0, elementComparables.First());
                        }
                    else
                        foreach (var elementComparables in e.NewItems!.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                            if (comparables.TryGetValue(elementComparables.Key, out var elementComparablesList))
                                AssignComparable(elementComparablesList, selectionIndex, elementComparables.First());
        }
    }
}
