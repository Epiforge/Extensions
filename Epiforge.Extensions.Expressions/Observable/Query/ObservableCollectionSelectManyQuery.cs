namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectManyQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, IEnumerable<TResult>>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver)
{
    readonly object access = new();
    int count;
    readonly Dictionary<IEnumerable<TResult>, List<PrefixWeightedSequenceNode<IEnumerable<TResult>?>>> enumerableNodes = [];
    List<TResult>? enumerationSnapshot;
    readonly PrefixWeightedSequence<IEnumerable<TResult>?> positions = new();
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<IEnumerable<TResult>>? select;
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
                enumerable = spanningEnumerable;
                offset = index - positions.PrefixWeightBefore(node);
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
            if (!enumerableNodes.TryGetValue(enumerable, out var nodes))
                return;
            enumerationSnapshot = null;
            var newWeight = enumerable.Count();
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
                    select!.CollectionChanged -= SelectCollectionChanged;
                    select.PropertyChanged -= SelectPropertyChanged;
                    select.PropertyChanging -= SelectPropertyChanging;
                    select.Dispose();
                    RemovedFromCache();
                }
            return removedFromCache;
        }
        return true;
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
                        results.AddRange(enumerable);
                enumerationSnapshot = results;
            }
            return enumerationSnapshot.GetEnumerator();
        }
    }

    void ObserveProjectionWithAccess(int index, IEnumerable<TResult>? enumerable)
    {
        var node = positions.Insert(index, enumerable, enumerable?.Count() ?? 0);
        if (enumerable is null)
            return;
        if (enumerableNodes.TryGetValue(enumerable, out var nodes))
            nodes.Add(node);
        else
        {
            enumerableNodes.Add(enumerable, [node]);
            if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            select = source.ObserveSelect(Selector);
            OperationFault = select.OperationFault;
            for (int i = 0, ii = select.Count; i < ii; ++i)
                ObserveProjectionWithAccess(positions.Count, select[i]);
            count = positions.TotalWeight;
            select.CollectionChanged += SelectCollectionChanged;
            select.PropertyChanged += SelectPropertyChanged;
            select.PropertyChanging += SelectPropertyChanging;
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
            if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
        }
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
                                oldItems.AddRange(oldEnumerable);
                            ReleaseProjectionWithAccess(node);
                        }
                    var newItems = new List<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var newEnumerable = e.NewItems[i] as IEnumerable<TResult>;
                            ObserveProjectionWithAccess(e.NewStartingIndex + i, newEnumerable);
                            if (newEnumerable is not null)
                                newItems.AddRange(newEnumerable);
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
                                movedItems.AddRange(movedEnumerable);
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

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            OperationFault = select!.OperationFault;
            OnPropertyChanged(e);
        }
    }

    void SelectPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            OnPropertyChanging(e);
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
