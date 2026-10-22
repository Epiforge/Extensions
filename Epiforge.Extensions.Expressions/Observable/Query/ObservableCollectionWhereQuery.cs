namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionWhereQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, bool>> predicate) :
    ObservableCollectionQuery<TElement>(collectionObserver),
    IObservableQueryDependent
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
    PropertyChangedEventHandler? observableExpressionPropertyChangedHandler;
    /// <summary>
    /// What the query keeps of each observation of an element, made with the first element, since a query built over a source with nothing in it often never has one
    /// </summary>
    Dictionary<IObservableExpression<TElement, bool>, (NodeSet<IObservableExpression<TElement, bool>> Nodes, Exception? Fault)>? observableExpressionStates;
    internal readonly Expression<Func<TElement, bool>> Predicate = predicate;
    bool released;
    ObservableQuerySubscription? sourceSubscription;

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

    private protected override int CurrentCount =>
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
                lock (access)
                {
                    released = true;
                    if (observableExpressionStates is { } states)
                        foreach (var (observableExpression, state) in states)
                        {
                            observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                            for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                                observableExpression.Dispose();
                        }
                    source.UnsubscribeDependent(sourceSubscription!);
                    enumerationSnapshot = null;
                    enumerationSnapshotShared = false;
                }
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
        var isPatchingSnapshot = TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot);
        var isChangeObserved = IsChangeObserved;
        var translatedIndex = 0;
        if (isPatchingSnapshot || isChangeObserved)
            memberships.SetWeight(node, newWeight, out translatedIndex);
        else
            memberships.SetWeight(node, newWeight);
        cursorNode = null;
        if (isPatchingSnapshot)
        {
            if (newResult)
                snapshot!.Insert(translatedIndex, node.Item.Argument);
            else
                snapshot!.RemoveAt(translatedIndex);
        }
        count += newResult ? 1 : -1;
        if (isChangeObserved)
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(newResult ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove, node.Item.Argument, translatedIndex));
        else
            AnnounceCountOfUnobservedChange();
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
        {
            var snapshot = UseEnumerationSnapshotWithAccess();
            enumerationSnapshotShared = true;
            Interlocked.Increment(ref liveEnumerations);
            return new SnapshotEnumerator(this, snapshot);
        }
    }

    /// <summary>
    /// Yields the one handler this query attaches to every observation it makes, since a method group converts to a new delegate at each conversion and this one is converted twice for every element
    /// </summary>
    PropertyChangedEventHandler ObservableExpressionPropertyChangedHandler =>
        observableExpressionPropertyChangedHandler ??= ObservableExpressionPropertyChanged;

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, bool> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (released)
                return;
            if (observableExpressionStates is not { } states || !states.TryGetValue(observableExpression, out var state))
                return;
            var (newFault, newResult) = observableExpression.Evaluation;
            if (FaultList.ExchangeElementFault(OwnOperationFault, observableExpression.Argument, elementComparer, state.Fault, newFault, out var newOperationFault))
            {
                states[observableExpression] = (state.Nodes, newFault);
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

    /// <summary>
    /// Observes an element, subscribing to the first observation of it before reading it, so that a change made on another thread in between is taken up once the lock is released rather than lost
    /// </summary>
    void ObserveElementWithAccess(TElement element, ref FaultList? faultList, ref int runningCount)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
        ref var state = ref StateOfWithAccess(observableExpression);
        var isObserved = !Unsafe.IsNullRef(ref state);
        if (!isObserved)
            observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
        var (fault, result) = observableExpression.Evaluation;
        var node = memberships.Insert(memberships.Count, observableExpression, result ? 1 : 0);
        if (fault is not null)
            (faultList ??= new()).Add(new EvaluationFaultException(element, fault));
        if (result)
            ++runningCount;
        if (isObserved)
            state.Nodes.Add(node);
        else
            (observableExpressionStates ??= []).Add(observableExpression, (new(node), fault));
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            FaultList? faultList = null;
            var runningCount = 0;
            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    ObserveElementWithAccess(source[i], ref faultList, ref runningCount);
            else
                foreach (var element in source)
                    ObserveElementWithAccess(element, ref faultList, ref runningCount);
            count = runningCount;
            OperationFault = faultList?.Fault;
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    /// <summary>
    /// Finds what the query keeps of an observation, which is a null reference where it keeps nothing of it, including where it has yet to keep anything at all
    /// </summary>
    ref (NodeSet<IObservableExpression<TElement, bool>> Nodes, Exception? Fault) StateOfWithAccess(IObservableExpression<TElement, bool> observableExpression)
    {
        if (observableExpressionStates is { } states)
            return ref CollectionsMarshal.GetValueRefOrNullRef(states, observableExpression);
        return ref Unsafe.NullRef<(NodeSet<IObservableExpression<TElement, bool>> Nodes, Exception? Fault)>();
    }

    void ReleaseMembershipWithAccess(PrefixWeightedSequenceNode<IObservableExpression<TElement, bool>> node)
    {
        var observableExpression = node.Item;
        ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(observableExpressionStates!, observableExpression);
        state.Nodes.Remove(node);
        if (state.Nodes.Count == 0)
        {
            observableExpressionStates!.Remove(observableExpression);
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
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

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e) =>
        SourceCollectionChanged(e);

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

    void SourceCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            cursorNode = null;
            FaultList? faultList = null;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            var newCount = 0;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new ChangedElements<TElement>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                        for (var i = e.OldItems.Count - 1; i >= 0; --i)
                        {
                            var element = (TElement)e.OldItems[i]!;
                            var node = memberships.RemoveAt(e.OldStartingIndex + i);
                            var observableExpression = node.Item;
                            var fault = observableExpressionStates![observableExpression].Fault;
                            ReleaseMembershipWithAccess(node);
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OwnOperationFault);
                                faultList.RemoveElementOccurrence(observableExpression.Argument, elementComparer);
                            }
                            if (node.Weight == 1)
                                oldItems.Add(element);
                            observableExpression.Dispose();
                        }
                    oldItems.Reverse();
                    var newItems = new ChangedElements<TElement>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                            ref var state = ref StateOfWithAccess(observableExpression);
                            var isObserved = !Unsafe.IsNullRef(ref state);
                            if (!isObserved)
                                observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
                            var (fault, result) = observableExpression.Evaluation;
                            var node = memberships.Insert(e.NewStartingIndex + i, observableExpression, result ? 1 : 0);
                            if (isObserved)
                                state.Nodes.Add(node);
                            else
                                (observableExpressionStates ??= []).Add(observableExpression, (new(node), fault));
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OwnOperationFault);
                                faultList.Check(observableExpression);
                            }
                            if (result)
                                newItems.Add(element);
                        }
                    if (newItems.Count > 0)
                    {
                        var translatedIndex = memberships.PrefixWeightBefore(e.NewStartingIndex);
                        eventArgs = oldItems.Count > 0 ? newItems.ToReplacementOf(oldItems, translatedIndex) : newItems.ToAddition(translatedIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                        {
                            if (oldItems.Count > 0)
                                snapshot.RemoveRange(translatedIndex, oldItems.Count);
                            newItems.InsertInto(snapshot, translatedIndex);
                        }
                    }
                    else if (oldItems.Count > 0)
                    {
                        var translatedIndex = memberships.PrefixWeightBefore(e.OldStartingIndex);
                        eventArgs = oldItems.ToRemoval(translatedIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                            snapshot.RemoveRange(translatedIndex, oldItems.Count);
                    }
                    newCount = count + newItems.Count - oldItems.Count;
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0)
                    {
                        var oldStartingIndex = memberships.PrefixWeightBefore(e.OldStartingIndex);
                        var movedItems = new ChangedElements<TElement>();
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
                            eventArgs = movedItems.ToMove(newStartingIndex, oldStartingIndex);
                            if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot))
                            {
                                snapshot.RemoveRange(oldStartingIndex, movedItems.Count);
                                movedItems.InsertInto(snapshot, newStartingIndex);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    faultList = new FaultList();
                    if (observableExpressionStates is { } resetStates)
                    {
                        foreach (var (observableExpression, state) in resetStates)
                        {
                            observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                            for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                                observableExpression.Dispose();
                        }
                        resetStates.Clear();
                    }
                    memberships.Clear();
                    enumerationSnapshot = null;
                    enumerationSnapshotShared = false;
                    if (!source.HasIndexerPenalty)
                        for (int i = 0, ii = source.Count; i < ii; ++i)
                            ObserveElementWithAccess(source[i], ref faultList, ref newCount);
                    else
                        foreach (var element in source)
                            ObserveElementWithAccess(element, ref faultList, ref newCount);
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
                    count = newCount;
                OnCollectionChanged(eventArgs);
            }
        }
    }

    public override string ToString() =>
        $"{source} matching {Predicate}";

    private protected override bool TryCopyTo(TElement[] array, int arrayIndex)
    {
        lock (access)
        {
            if (array.Length - arrayIndex < count)
                return false;
            UseEnumerationSnapshotWithAccess().CopyTo(array, arrayIndex);
            return true;
        }
    }

    /// <summary>
    /// Gets the snapshot of the elements kept for reading them in order, taking one where none is kept, and restarts the count of changes it may be patched for, since it is being read
    /// </summary>
    List<TElement> UseEnumerationSnapshotWithAccess()
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
        return enumerationSnapshot;
    }
}
