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
            foreach (var (selection, isDescending) in selectionsAndDirections.Take(1))
            {
                foreach (var elementComparables in selection.GroupBy(t => t.Item1, t => t.Item2))
                {
                    var element = elementComparables.Key;
                    var elementComparablesList = new List<IComparable>();
                    comparables.Add(element, elementComparablesList);
                    elementComparablesList.Add(elementComparables.First());
                    counts.Add(element, elementComparables.Count());
                }
            }
            foreach (var (selection, isDescending) in selectionsAndDirections.Skip(1))
                foreach (var elementComparables in selection.GroupBy(t => t.Item1, t => t.Item2))
                    comparables[elementComparables.Key].Add(elementComparables.First());
        }
    }

    readonly object access;
    readonly NullableKeyDictionary<TElement, List<IComparable>> comparables;
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
        IReadOnlyList<IComparable?> xList, yList;
        if (comparables.TryGetValue(x!, out var rawXList))
            xList = rawXList;
        else
            xList = Enumerable.Range(0, selectionsAndDirections.Count).Select(i => (IComparable?)null).ToList().AsReadOnly();
        if (comparables.TryGetValue(y!, out var rawYList))
            yList = rawYList;
        else
            yList = Enumerable.Range(0, selectionsAndDirections.Count).Select(i => (IComparable?)null).ToList().AsReadOnly();
        for (var i = 0; i < selectionsAndDirections.Count; ++i)
        {
            var isDescending = selectionsAndDirections[i].isDescending;
            var xComparable = xList[i];
            var yComparable = yList[i];
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

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    void SelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset && ReferenceEquals(sender, lastSelectionAndDirection.selection))
            {
                comparables.Clear();
                counts.Clear();
                foreach (var (selection, isDescending) in selectionsAndDirections.Take(1))
                {
                    foreach (var elementComparables in selection.GroupBy(t => t.Item1, t => t.Item2))
                    {
                        var element = elementComparables.Key;
                        var elementComparablesList = new List<IComparable>();
                        comparables.Add(element, elementComparablesList);
                        elementComparablesList.Add(elementComparables.First());
                        counts.Add(element, elementComparables.Count());
                    }
                }
                foreach (var (selection, isDescending) in selectionsAndDirections.Skip(1))
                    foreach (var elementComparables in selection.GroupBy(t => t.Item1, t => t.Item2))
                        comparables[elementComparables.Key].Add(elementComparables.First());
                comparables.TrimExcess();
                counts.TrimExcess();
            }
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
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
                        var currentCount = counts[element];
                        var removedCount = elementComparables.Count();
                        var countDiff = currentCount - removedCount;
                        if (countDiff == 0)
                        {
                            counts.Remove(element);
                            comparables.Remove(element);
                        }
                        else
                            counts[element] = countDiff;
                    }
                if ((e.NewItems?.Count ?? 0) > 0)
                {
                    var selectionIndicies = selectionsAndDirections.FindIndicies(t => ReferenceEquals(t.selection, sender));
                    foreach (var selectionIndex in selectionIndicies)
                    {
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
                                AssignComparable(comparables[elementComparables.Key], selectionIndex, elementComparables.First());
                    }
                }
            }
        }
    }
}
