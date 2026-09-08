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

    /// <summary>
    /// Records the comparable of the first selection for the specified number of occurrences of an element, which begins keeping that element where it is not yet kept
    /// </summary>
    void AssignFirstComparable(TElement element, int addedCount, IComparable comparable)
    {
        if (!comparables.TryGetValue(element, out var elementComparablesList))
        {
            elementComparablesList = [];
            comparables.Add(element, elementComparablesList);
            counts.Add(element, addedCount);
        }
        else
            counts[element] += addedCount;
        AssignComparable(elementComparablesList, 0, comparable);
    }

    /// <summary>
    /// Records the comparable of a selection other than the first for an element already kept, which is where an ordering by more than one key keeps its later keys
    /// </summary>
    void AssignLaterComparable(TElement element, int selectionIndex, IComparable comparable)
    {
        if (comparables.TryGetValue(element, out var elementComparablesList))
            AssignComparable(elementComparablesList, selectionIndex, comparable);
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

    /// <summary>
    /// Forgets the specified number of occurrences of an element, which stops keeping it where none remain
    /// </summary>
    void RemoveOccurrences(TElement element, int removedCount)
    {
        if (!counts.TryGetValue(element, out var currentCount))
            return;
        var countDiff = currentCount - removedCount;
        if (countDiff <= 0)
        {
            counts.Remove(element);
            comparables.Remove(element);
        }
        else
            counts[element] = countDiff;
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

    /// <remarks>
    /// The selections are walked by index rather than searched with a predicate, because the predicate closes over the sender and so allocates a closure, a delegate and an iterator on every change this handles, which for a query ordered on an observed key is once for every key change for the life of the query, over a list which is nearly always one selection long
    /// </remarks>
    /// <remarks>
    /// A payload of a single item is handled without grouping it, because grouping one item builds a lookup, its array of groupings, the grouping itself and the array of its one element, none of which a payload of one needs. The grouping remains for payloads which can hold one element more than once
    /// </remarks>
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
                    for (int selectionIndex = 0, selectionCount = selectionsAndDirections.Count; selectionIndex < selectionCount; ++selectionIndex)
                        if (ReferenceEquals(selectionsAndDirections[selectionIndex].selection, sender))
                            AssignComparable(replacedComparables, selectionIndex, newItem.Item2);
                return;
            }
            if ((e.OldItems?.Count ?? 0) > 0 && ReferenceEquals(sender, lastSelectionAndDirection.selection))
            {
                var oldItems = e.OldItems!;
                if (oldItems.Count == 1)
                {
                    if (oldItems[0] is Tuple<TElement, IComparable> onlyOldItem)
                        RemoveOccurrences(onlyOldItem.Item1, 1);
                }
                else
                    foreach (var elementComparables in oldItems.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                        RemoveOccurrences(elementComparables.Key, elementComparables.Count());
            }
            if ((e.NewItems?.Count ?? 0) > 0)
            {
                var newItems = e.NewItems!;
                var onlyNewItem = newItems.Count == 1 ? newItems[0] as Tuple<TElement, IComparable> : null;
                for (int selectionIndex = 0, selectionCount = selectionsAndDirections.Count; selectionIndex < selectionCount; ++selectionIndex)
                {
                    if (!ReferenceEquals(selectionsAndDirections[selectionIndex].selection, sender))
                        continue;
                    if (onlyNewItem is not null)
                    {
                        if (selectionIndex == 0)
                            AssignFirstComparable(onlyNewItem.Item1, 1, onlyNewItem.Item2);
                        else
                            AssignLaterComparable(onlyNewItem.Item1, selectionIndex, onlyNewItem.Item2);
                    }
                    else if (selectionIndex == 0)
                        foreach (var elementComparables in newItems.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                            AssignFirstComparable(elementComparables.Key, elementComparables.Count(), elementComparables.First());
                    else
                        foreach (var elementComparables in newItems.OfType<Tuple<TElement, IComparable>>().GroupBy(t => t.Item1, t => t.Item2))
                            AssignLaterComparable(elementComparables.Key, selectionIndex, elementComparables.First());
                }
            }
        }
    }
}
