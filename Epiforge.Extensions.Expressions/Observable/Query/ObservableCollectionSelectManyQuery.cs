namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectManyQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, IEnumerable<TResult>>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver),
    IObservableQueryDependent
{
    readonly object access = new();
    Dictionary<IEnumerable<TResult>, List<TResult>>? copies;
    int count;
    readonly Dictionary<IEnumerable<TResult>, List<PrefixWeightedSequenceNode<IEnumerable<TResult>?>>> enumerableNodes = [];
    List<TResult>? enumerationSnapshot;
    readonly PrefixWeightedSequence<IEnumerable<TResult>?> positions = new();
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<IEnumerable<TResult>>? select;
    ObservableQuerySubscription? selectSubscription;
    internal readonly Expression<Func<TElement, IEnumerable<TResult>>> Selector = selector;

    public override TResult this[int index]
    {
        get
        {
            IEnumerable<TResult> enumerable;
            int offset;
            lock (access)
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException();
                if (enumerationSnapshot is { } snapshot)
                    return snapshot[index];
                if (positions.NodeAtWeight(index) is not { } node || node.Item is not { } spanningEnumerable)
                    throw new IndexOutOfRangeException();
                offset = index - positions.PrefixWeightBefore(node);
                if (copies is not null && copies.TryGetValue(spanningEnumerable, out var copy))
                    return copy[offset];
                enumerable = spanningEnumerable;
            }
            return enumerable.ElementAt(offset);
        }
    }

    public override int Count =>
        count;

    internal override bool HasIndexerPenalty =>
        true;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IEnumerable<TResult> enumerable)
            return;
        lock (access)
        {
            if (!enumerableNodes.TryGetValue(enumerable, out var nodes) || copies is null || !copies.TryGetValue(enumerable, out var copy))
                return;
            enumerationSnapshot = null;
            copies[enumerable] = copy = FollowInnerChange(copy, e, enumerable);
            var newWeight = copy.Count;
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    positions.SetWeight(nodes[i], newWeight);
                SetCount(positions.TotalWeight);
                OnCollectionChanged(e);
                return;
            }
            if (nodes.Count == 1)
                TranslateInnerChangeWithAccess(nodes[0], newWeight, e);
            else
            {
                var orderedNodes = new List<(int Index, PrefixWeightedSequenceNode<IEnumerable<TResult>?> Node)>(nodes.Count);
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    orderedNodes.Add((positions.IndexOf(nodes[i]), nodes[i]));
                orderedNodes.Sort((first, second) => first.Index.CompareTo(second.Index));
                for (int i = 0, ii = orderedNodes.Count; i < ii; ++i)
                    TranslateInnerChangeWithAccess(orderedNodes[i].Node, newWeight, e);
            }
            SetCount(positions.TotalWeight);
        }
    }

    /// <summary>
    /// Gets what this query has been told the specified sequence holds, which is its copy of it where the sequence announces its changes
    /// </summary>
    IEnumerable<TResult> ContentsWithAccess(IEnumerable<TResult> enumerable) =>
        copies is not null && copies.TryGetValue(enumerable, out var copy) ? copy : enumerable;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
                lock (access)
                {
                    foreach (var enumerable in enumerableNodes.Keys)
                        if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                            collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                    ((ScopedObservableCollectionQuery<IEnumerable<TResult>>)select!).query.UnsubscribeDependent(selectSubscription!);
                    select.Dispose();
                    RemovedFromCache();
                }
            return removedFromCache;
        }
        return true;
    }

    /// <summary>
    /// Applies an announced change to a copy of the sequence which announced it, reading the sequence again only when the change cannot be applied to the copy
    /// </summary>
    static List<TResult> FollowInnerChange(List<TResult> copy, NotifyCollectionChangedEventArgs e, IEnumerable<TResult> enumerable)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { } newItems && e.NewStartingIndex >= 0 && e.NewStartingIndex <= copy.Count:
                if (newItems.Count == 1)
                    copy.Insert(e.NewStartingIndex, (TResult)newItems[0]!);
                else
                    copy.InsertRange(e.NewStartingIndex, newItems.Cast<TResult>());
                return copy;
            case NotifyCollectionChangedAction.Remove when e.OldItems is { } oldItems && e.OldStartingIndex >= 0 && e.OldStartingIndex + oldItems.Count <= copy.Count:
                copy.RemoveRange(e.OldStartingIndex, oldItems.Count);
                return copy;
            case NotifyCollectionChangedAction.Replace when e.OldItems is { } replacedItems && e.NewItems is { } replacementItems && e.OldStartingIndex >= 0 && e.OldStartingIndex + replacedItems.Count <= copy.Count:
                copy.RemoveRange(e.OldStartingIndex, replacedItems.Count);
                copy.InsertRange(e.OldStartingIndex, replacementItems.Cast<TResult>());
                return copy;
            case NotifyCollectionChangedAction.Move when e.OldItems is { } movedItems && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex + movedItems.Count <= copy.Count && e.NewStartingIndex + movedItems.Count <= copy.Count:
                var moved = copy.GetRange(e.OldStartingIndex, movedItems.Count);
                copy.RemoveRange(e.OldStartingIndex, movedItems.Count);
                copy.InsertRange(e.NewStartingIndex, moved);
                return copy;
            default:
                return [.. enumerable];
        }
    }

    public override IEnumerator<TResult> GetEnumerator()
    {
        lock (access)
        {
            if (enumerationSnapshot is null)
            {
                var results = new List<TResult>(count);
                for (var node = positions.FirstNode; node is not null; node = positions.Next(node))
                    if (node.Item is { } enumerable)
                        results.AddRange(ContentsWithAccess(enumerable));
                enumerationSnapshot = results;
            }
            return enumerationSnapshot.GetEnumerator();
        }
    }

    void ObserveProjectionWithAccess(int index, IEnumerable<TResult>? enumerable)
    {
        if (enumerable is null)
        {
            positions.Insert(index, enumerable, 0);
            return;
        }
        if (enumerableNodes.TryGetValue(enumerable, out var nodes))
            nodes.Add(positions.Insert(index, enumerable, ContentsWithAccess(enumerable).Count()));
        else if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
        {
            List<TResult> copy = [.. enumerable];
            (copies ??= []).Add(enumerable, copy);
            enumerableNodes.Add(enumerable, [positions.Insert(index, enumerable, copy.Count)]);
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
        else
            enumerableNodes.Add(enumerable, [positions.Insert(index, enumerable, enumerable.Count())]);
    }

    protected override void OnInitialization()
    {
        var projections = source.ObserveSelect(Selector);
        using var changeHold = HoldChangesOf(((ScopedObservableCollectionQuery<IEnumerable<TResult>>)projections).query);
        lock (access)
        {
            select = projections;
            OperationFault = select.OperationFault;
            for (int i = 0, ii = select.Count; i < ii; ++i)
                ObserveProjectionWithAccess(positions.Count, select[i]);
            count = positions.TotalWeight;
            selectSubscription = ((ScopedObservableCollectionQuery<IEnumerable<TResult>>)select).query.SubscribeDependent(this);
        }
    }

    void ReleaseProjectionWithAccess(PrefixWeightedSequenceNode<IEnumerable<TResult>?> node)
    {
        if (node.Item is not { } enumerable || !enumerableNodes.TryGetValue(enumerable, out var nodes))
            return;
        nodes.Remove(node);
        if (nodes.Count == 0)
        {
            enumerableNodes.Remove(enumerable);
            copies?.Remove(enumerable);
            if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
        }
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e) =>
        SelectCollectionChanged(e);

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            OperationFault = select!.OperationFault;
            OnPropertyChanged(e);
        }
    }

    void IObservableQueryDependent.OnDependencyPropertyChanging(ObservableQuerySubscription subscription, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            OnPropertyChanging(e);
        }
    }

    void SelectCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            enumerationSnapshot = null;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    var oldItems = new List<TResult>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                        for (var i = 0; i < e.OldItems.Count; ++i)
                        {
                            var node = positions.RemoveAt(e.OldStartingIndex);
                            if (node.Item is { } oldEnumerable)
                                oldItems.AddRange(ContentsWithAccess(oldEnumerable));
                            ReleaseProjectionWithAccess(node);
                        }
                    var newItems = new List<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var newEnumerable = e.NewItems[i] as IEnumerable<TResult>;
                            ObserveProjectionWithAccess(e.NewStartingIndex + i, newEnumerable);
                            if (newEnumerable is not null)
                                newItems.AddRange(ContentsWithAccess(newEnumerable));
                        }
                    if (oldItems.Count > 0)
                    {
                        var reducedOldStartingIndex = positions.PrefixWeightBefore(e.OldStartingIndex);
                        if (e.OldStartingIndex > e.NewStartingIndex)
                            reducedOldStartingIndex += newItems.Count;
                        if (newItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), reducedOldStartingIndex);
                        else
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), reducedOldStartingIndex);
                    }
                    else if (newItems.Count > 0)
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), positions.PrefixWeightBefore(e.NewStartingIndex));
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0 && e.OldStartingIndex != e.NewStartingIndex)
                    {
                        var reducedOldStartingIndex = positions.PrefixWeightBefore(e.OldStartingIndex);
                        var movedItems = new List<TResult>();
                        var movedNode = positions.NodeAt(e.OldStartingIndex);
                        for (var i = 0; i < e.OldItems.Count && movedNode is not null; ++i)
                        {
                            if (movedNode.Item is { } movedEnumerable)
                                movedItems.AddRange(ContentsWithAccess(movedEnumerable));
                            movedNode = positions.Next(movedNode);
                        }
                        positions.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        var reducedNewStartingIndex = positions.PrefixWeightBefore(e.NewStartingIndex);
                        if (reducedOldStartingIndex != reducedNewStartingIndex && movedItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems.AsReadOnly(), reducedNewStartingIndex, reducedOldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var enumerable in enumerableNodes.Keys)
                        if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                            collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                    enumerableNodes.Clear();
                    copies?.Clear();
                    positions.Clear();
                    for (int i = 0, ii = select!.Count; i < ii; ++i)
                        ObserveProjectionWithAccess(positions.Count, select[i]);
                    eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                    break;
                default:
                    throw new NotSupportedException($"collection changed action {e.Action} is not supported");
            }
            if (eventArgs is not null)
            {
                if (eventArgs.Action is not NotifyCollectionChangedAction.Move)
                    SetCount(positions.TotalWeight);
                OnCollectionChanged(eventArgs);
            }
        }
    }

    void SetCount(int value) =>
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);

    public override string ToString() =>
        $"reduction of mapping {source} with {Selector}";

    void TranslateInnerChangeWithAccess(PrefixWeightedSequenceNode<IEnumerable<TResult>?> node, int newWeight, NotifyCollectionChangedEventArgs e)
    {
        positions.SetWeight(node, newWeight, out var reducedIndex);
        OnCollectionChanged(e.Action switch
        {
            NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems, reducedIndex + e.NewStartingIndex),
            NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems ?? e.OldItems, reducedIndex + e.NewStartingIndex, reducedIndex + e.OldStartingIndex),
            NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(e.Action, e.OldItems, reducedIndex + e.OldStartingIndex),
            NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems!, e.OldItems!, reducedIndex + e.OldStartingIndex),
            _ => throw new NotSupportedException($"collection changed action {e.Action} is not supported")
        });
    }
}
