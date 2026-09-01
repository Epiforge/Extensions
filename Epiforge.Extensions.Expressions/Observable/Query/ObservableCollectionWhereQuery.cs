namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionWhereQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, bool>> predicate) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    sealed class SnapshotEnumerator :
        IEnumerator<TElement>
    {
        internal SnapshotEnumerator(ObservableCollectionWhereQuery<TElement> query, List<TElement> snapshot)
        {
            this.query = query;
            this.snapshot = snapshot;
            enumerator = snapshot.GetEnumerator();
        }

        List<TElement>.Enumerator enumerator;
        bool isDisposed;
        readonly ObservableCollectionWhereQuery<TElement> query;
        readonly List<TElement> snapshot;

        public TElement Current =>
            enumerator.Current;

        object? IEnumerator.Current =>
            enumerator.Current;

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            enumerator.Dispose();
            query.EnumerationEnded();
        }

        public bool MoveNext() =>
            enumerator.MoveNext();

        public void Reset() =>
            enumerator = snapshot.GetEnumerator();
    }

    const int maximumEnumerationSnapshotPatches = 128;

    readonly object access = new();
    int count;
    int cursorIndex = -1;
    PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>>? cursorNode;
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    List<TElement>? enumerationSnapshot;
    int enumerationSnapshotPatches;
    bool enumerationSnapshotShared;
    int liveEnumerations;
    readonly PrefixWeightedSequence<IObservableExpression<TElement, bool>> memberships = new();
    readonly Dictionary<IObservableExpression<TElement, bool>, (List<PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>>> Nodes, Exception? Fault)> observableExpressionStates = [];
    internal readonly Expression<Func<TElement, bool>> Predicate = predicate;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
            {
                if (index < 0 || index >= count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                if (enumerationSnapshot is { } snapshot)
                    return snapshot[index];
                var node = cursorNode is { } finger ? memberships.NodeAtWeightFrom(finger, cursorIndex, index) : memberships.NodeAtWeight(index);
                if (node is null)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                cursorIndex = index;
                cursorNode = node;
                return node.Item.Argument;
            }
        }
    }

    public override int Count =>
        count;

    internal override bool HasIndexerPenalty =>
        true;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                foreach (var (observableExpression, state) in observableExpressionStates)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                        observableExpression.Dispose();
                }
                source.CollectionChanged -= SourceCollectionChanged;
                enumerationSnapshot = null;
                enumerationSnapshotShared = false;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void EnumerationEnded() =>
        Interlocked.Decrement(ref liveEnumerations);

    void FlipMembershipWithAccess(PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>> node, bool newResult)
    {
        var newWeight = newResult ? 1 : 0;
        if (node.Weight == newWeight)
            return;
        var translatedIndex = memberships.SetWeight(node, newWeight);
        cursorNode = null;
        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
        {
            if (newResult)
                snapshot.Insert(translatedIndex, node.Item.Argument);
            else
                snapshot.RemoveAt(translatedIndex);
        }
        SetCount(count + (newResult ? 1 : -1));
        if (IsChangeObserved)
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(newResult ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove, node.Item.Argument, translatedIndex));
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
        {
            if (enumerationSnapshot is null)
            {
                var elements = new List<TElement>(count);
                for (var node = memberships.FirstNode; node is not null; node = memberships.Next(node))
                    if (node.Weight == 1)
                        elements.Add(node.Item.Argument);
                enumerationSnapshot = elements;
            }
            enumerationSnapshotPatches = 0;
            enumerationSnapshotShared = true;
            Interlocked.Increment(ref liveEnumerations);
            return new SnapshotEnumerator(this, enumerationSnapshot);
        }
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, bool> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (!observableExpressionStates.TryGetValue(observableExpression, out var state))
                return;
            var (newFault, newResult) = observableExpression.Evaluation;
            if (FaultList.ExchangeElementFault(OperationFault, observableExpression.Argument, elementComparer, state.Fault, newFault, out var newOperationFault))
            {
                observableExpressionStates[observableExpression] = (state.Nodes, newFault);
                OperationFault = newOperationFault;
            }
            var nodes = state.Nodes;
            if (nodes.Count == 1)
                FlipMembershipWithAccess(nodes[0], newResult);
            else
            {
                var orderedNodes = new List<(int Index, PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>> Node)>(nodes.Count);
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    orderedNodes.Add((memberships.IndexOf(nodes[i]), nodes[i]));
                orderedNodes.Sort((first, second) => first.Index.CompareTo(second.Index));
                for (int i = 0, ii = orderedNodes.Count; i < ii; ++i)
                    FlipMembershipWithAccess(orderedNodes[i].Node, newResult);
            }
        }
    }

    void ObserveElementWithAccess(TElement element, FaultList faultList, ref int runningCount)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
        var (fault, result) = observableExpression.Evaluation;
        var node = memberships.Insert(memberships.Count, observableExpression, result ? 1 : 0);
        faultList.Check(observableExpression);
        if (result)
            ++runningCount;
        if (observableExpressionStates.TryGetValue(observableExpression, out var state))
            state.Nodes.Add(node);
        else
        {
            observableExpressionStates.Add(observableExpression, ([node], fault));
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        }
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var faultList = new FaultList();
            var runningCount = 0;
            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    ObserveElementWithAccess(source[i], faultList, ref runningCount);
            else
                foreach (var element in source)
                    ObserveElementWithAccess(element, faultList, ref runningCount);
            count = runningCount;
            OperationFault = faultList.Fault;
            source.CollectionChanged += SourceCollectionChanged;
        }
    }

    void ReleaseMembershipWithAccess(PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>> node)
    {
        var observableExpression = node.Item;
        var state = observableExpressionStates[observableExpression];
        state.Nodes.Remove(node);
        if (state.Nodes.Count == 0)
        {
            observableExpressionStates.Remove(observableExpression);
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
        }
    }

    bool TryBeginEnumerationSnapshotPatchWithAccess([NotNullWhen(true)] out List<TElement>? snapshot)
    {
        if (enumerationSnapshot is { } existing && ++enumerationSnapshotPatches <= maximumEnumerationSnapshotPatches)
        {
            if (enumerationSnapshotShared)
            {
                if (Volatile.Read(ref liveEnumerations) > 0)
                {
                    existing = new List<TElement>(existing);
                    enumerationSnapshot = existing;
                }
                enumerationSnapshotShared = false;
            }
            snapshot = existing;
            return true;
        }
        enumerationSnapshot = null;
        enumerationSnapshotShared = false;
        snapshot = null;
        return false;
    }

    void SetCount(int value) =>
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            cursorNode = null;
            FaultList? faultList = null;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            var newCount = 0;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new List<TElement>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                        for (var i = e.OldItems.Count - 1; i >= 0; --i)
                        {
                            var element = (TElement)e.OldItems[i]!;
                            var node = memberships.RemoveAt(e.OldStartingIndex + i);
                            var observableExpression = node.Item;
                            var fault = observableExpressionStates[observableExpression].Fault;
                            ReleaseMembershipWithAccess(node);
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.RemoveElementOccurrence(observableExpression.Argument, elementComparer);
                            }
                            if (node.Weight == 1)
                                oldItems.Add(element);
                            observableExpression.Dispose();
                        }
                    var newItems = new List<TElement>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                            var (fault, result) = observableExpression.Evaluation;
                            var node = memberships.Insert(e.NewStartingIndex + i, observableExpression, result ? 1 : 0);
                            if (observableExpressionStates.TryGetValue(observableExpression, out var state))
                                state.Nodes.Add(node);
                            else
                            {
                                observableExpressionStates.Add(observableExpression, ([node], fault));
                                observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                            }
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.Check(observableExpression);
                            }
                            if (result)
                                newItems.Add(element);
                        }
                    if (newItems.Count > 0)
                    {
                        var translatedIndex = memberships.PrefixWeightBefore(e.NewStartingIndex);
                        if (oldItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), translatedIndex);
                        else
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), translatedIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                        {
                            if (oldItems.Count > 0)
                                snapshot.RemoveRange(translatedIndex, oldItems.Count);
                            snapshot.InsertRange(translatedIndex, newItems);
                        }
                    }
                    else if (oldItems.Count > 0)
                    {
                        var translatedIndex = memberships.PrefixWeightBefore(e.OldStartingIndex);
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), translatedIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                            snapshot.RemoveRange(translatedIndex, oldItems.Count);
                    }
                    newCount = count + newItems.Count - oldItems.Count;
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0)
                    {
                        var oldStartingIndex = memberships.PrefixWeightBefore(e.OldStartingIndex);
                        var movedItems = new List<TElement>();
                        var movedNode = memberships.NodeAt(e.OldStartingIndex);
                        for (var i = 0; i < e.OldItems.Count && movedNode is not null; ++i)
                        {
                            if (movedNode.Weight == 1)
                                movedItems.Add(movedNode.Item.Argument);
                            movedNode = memberships.Next(movedNode);
                        }
                        memberships.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        var newStartingIndex = memberships.PrefixWeightBefore(e.NewStartingIndex);
                        if (oldStartingIndex != newStartingIndex && movedItems.Count > 0)
                        {
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems.AsReadOnly(), newStartingIndex, oldStartingIndex);
                            if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                            {
                                snapshot.RemoveRange(oldStartingIndex, movedItems.Count);
                                snapshot.InsertRange(newStartingIndex, movedItems);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    faultList = new FaultList();
                    foreach (var (observableExpression, state) in observableExpressionStates)
                    {
                        observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                        for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                            observableExpression.Dispose();
                    }
                    memberships.Clear();
                    observableExpressionStates.Clear();
                    enumerationSnapshot = null;
                    enumerationSnapshotShared = false;
                    if (!source.HasIndexerPenalty)
                        for (int i = 0, ii = source.Count; i < ii; ++i)
                            ObserveElementWithAccess(source[i], faultList, ref newCount);
                    else
                        foreach (var element in source)
                            ObserveElementWithAccess(element, faultList, ref newCount);
                    eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                    break;
                default:
                    throw new NotSupportedException($"collection changed action {e.Action} is not supported");
            }
            if (faultList is not null)
                OperationFault = faultList.Fault;
            if (eventArgs is not null)
            {
                if (eventArgs.Action != NotifyCollectionChangedAction.Move)
                    SetCount(newCount);
                OnCollectionChanged(eventArgs);
            }
        }
    }

    public override string ToString() =>
        $"{source} matching {Predicate}";
}
