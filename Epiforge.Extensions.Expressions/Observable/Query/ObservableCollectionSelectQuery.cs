namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver),
    IObservableQueryDependent
{
    sealed class Projection(IObservableExpression<TElement, TResult> observableExpression, TResult committedResult)
    {
        internal TResult CommittedResult = committedResult;
        internal readonly IObservableExpression<TElement, TResult> ObservableExpression = observableExpression;
    }

    sealed class SnapshotEnumerator :
        IEnumerator<TResult>
    {
        internal SnapshotEnumerator(ObservableCollectionSelectQuery<TElement, TResult> query, List<TResult> snapshot)
        {
            this.query = query;
            this.snapshot = snapshot;
            enumerator = snapshot.GetEnumerator();
        }

        List<TResult>.Enumerator enumerator;
        bool isDisposed;
        readonly ObservableCollectionSelectQuery<TElement, TResult> query;
        readonly List<TResult> snapshot;

        public TResult Current =>
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
    int cursorIndex = -1;
    PrefixWeightedSequenceNode<Projection>? cursorNode;
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    List<TResult>? enumerationSnapshot;
    int enumerationSnapshotPatches;
    bool enumerationSnapshotShared;
    int liveEnumerations;
    PropertyChangedEventHandler? observableExpressionPropertyChangedHandler;
    /// <summary>
    /// What the query keeps of each observation of an element, made with the first element, since a query built over a source with nothing in it often never has one
    /// </summary>
    Dictionary<IObservableExpression<TElement, TResult>, (Projection Projection, NodeSet<Projection> Nodes, Exception? Fault)>? observableExpressionStates;
    readonly PrefixWeightedSequence<Projection> positions = new();
    bool released;
    readonly EqualityComparer<TResult> resultComparer = EqualityComparer<TResult>.Default;
    internal readonly Expression<Func<TElement, TResult>> Selector = selector;
    ObservableQuerySubscription? sourceSubscription;

    public override TResult this[int index]
    {
        get
        {
            lock (access)
            {
                if (index < 0 || index >= positions.Count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                if (enumerationSnapshot is { } snapshot)
                    return snapshot[index];
                var node = cursorNode is { } finger ? positions.NodeAtFrom(finger, cursorIndex, index) : positions.NodeAt(index);
                cursorIndex = index;
                cursorNode = node;
                return node.Item.CommittedResult;
            }
        }
    }

    private protected override int CountAtInitialization =>
        positions.Count;

    private protected override int CurrentCount
    {
        get
        {
            lock (access)
                return positions.Count;
        }
    }

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

    public override IEnumerator<TResult> GetEnumerator()
    {
        lock (access)
        {
            var snapshot = UseEnumerationSnapshotWithAccess();
            enumerationSnapshotShared = true;
            Interlocked.Increment(ref liveEnumerations);
            return new SnapshotEnumerator(this, snapshot);
        }
    }

    void InvalidateEnumerationSnapshotWithAccess()
    {
        enumerationSnapshot = null;
        enumerationSnapshotShared = false;
    }

    /// <summary>
    /// Yields the one handler this query attaches to every observation it makes, since a method group converts to a new delegate at each conversion and this one is converted twice for every element
    /// </summary>
    PropertyChangedEventHandler ObservableExpressionPropertyChangedHandler =>
        observableExpressionPropertyChangedHandler ??= ObservableExpressionPropertyChanged;

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, TResult> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
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
                states[observableExpression] = (state.Projection, state.Nodes, newFault);
                OperationFault = newOperationFault;
            }
            var committedResult = state.Projection.CommittedResult;
            if (resultComparer.Equals(committedResult, newResult))
                return;
            state.Projection.CommittedResult = newResult;
            var nodes = state.Nodes;
            if (nodes.Count == 1)
                ReplaceAtWithAccess(positions.IndexOf(nodes[0]), committedResult, newResult);
            else
            {
                var indicies = new List<int>(nodes.Count);
                for (int i = 0, ii = nodes.Count; i < ii; ++i)
                    indicies.Add(positions.IndexOf(nodes[i]));
                indicies.Sort();
                for (int i = 0, ii = indicies.Count; i < ii; ++i)
                    ReplaceAtWithAccess(indicies[i], committedResult, newResult);
            }
        }
    }

    /// <summary>
    /// Observes an element, subscribing to the first observation of it before reading it, so that a change made on another thread in between is taken up once the lock is released rather than lost
    /// </summary>
    void ObserveElementWithAccess(TElement element, int index, ref FaultList? faultList)
    {
        if (ObserveElementWithAccess(element, index) is { } fault)
            (faultList ??= new()).Add(new EvaluationFaultException(element, fault));
    }

    /// <summary>
    /// Observes an element as <see cref="ObserveElementWithAccess(TElement, int, ref FaultList)"/> does, leaving what to do with its fault to the caller, so that a caller which needs a list of faults only where there is one need not make one beforehand
    /// </summary>
    /// <returns>The fault of the element's projection, if it has one; otherwise, <c>null</c></returns>
    Exception? ObserveElementWithAccess(TElement element, int index)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
        ref var state = ref StateOfWithAccess(observableExpression);
        var isObserved = !Unsafe.IsNullRef(ref state);
        if (!isObserved)
            observableExpression.PropertyChanged += ObservableExpressionPropertyChangedHandler;
        var (fault, result) = observableExpression.Evaluation;
        if (isObserved)
            state.Nodes.Add(positions.Insert(index, state.Projection, 1));
        else
        {
            var projection = new Projection(observableExpression, result);
            (observableExpressionStates ??= []).Add(observableExpression, (projection, new(positions.Insert(index, projection, 1)), fault));
        }
        return fault;
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            FaultList? faultList = null;
            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    ObserveElementWithAccess(source[i], positions.Count, ref faultList);
            else
                foreach (var element in source)
                    ObserveElementWithAccess(element, positions.Count, ref faultList);
            OperationFault = faultList?.Fault;
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    /// <summary>
    /// Finds what the query keeps of an observation, which is a null reference where it keeps nothing of it, including where it has yet to keep anything at all
    /// </summary>
    ref (Projection Projection, NodeSet<Projection> Nodes, Exception? Fault) StateOfWithAccess(IObservableExpression<TElement, TResult> observableExpression)
    {
        if (observableExpressionStates is { } states)
            return ref CollectionsMarshal.GetValueRefOrNullRef(states, observableExpression);
        return ref Unsafe.NullRef<(Projection Projection, NodeSet<Projection> Nodes, Exception? Fault)>();
    }

    void ReleaseProjectionWithAccess(PrefixWeightedSequenceNode<Projection> node)
    {
        var observableExpression = node.Item.ObservableExpression;
        ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(observableExpressionStates!, observableExpression);
        state.Nodes.Remove(node);
        if (state.Nodes.Count == 0)
        {
            observableExpressionStates!.Remove(observableExpression);
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
        }
        observableExpression.Dispose();
    }

    void ReplaceAtWithAccess(int index, TResult oldResult, TResult newResult)
    {
        if (TryBeginEnumerationSnapshotPatchWithAccess(out var snapshot) && index < snapshot.Count)
            snapshot[index] = newResult;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newResult, oldResult, index));
    }

    NotifyCollectionChangedEventArgs ResetWithAccess()
    {
        if (observableExpressionStates is { } states)
        {
            foreach (var (observableExpression, state) in states)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChangedHandler;
                for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                    observableExpression.Dispose();
            }
            states.Clear();
        }
        positions.Clear();
        InvalidateEnumerationSnapshotWithAccess();
        FaultList? faultList = null;
        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                ObserveElementWithAccess(source[i], positions.Count, ref faultList);
        else
            foreach (var element in source)
                ObserveElementWithAccess(element, positions.Count, ref faultList);
        OperationFault = faultList?.Fault;
        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
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
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new ChangedElements<TResult>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > positions.Count)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        for (var i = e.OldItems.Count - 1; i >= 0; --i)
                        {
                            var node = positions.RemoveAt(e.OldStartingIndex + i);
                            oldItems.Add(node.Item.CommittedResult);
                            var observableExpression = node.Item.ObservableExpression;
                            var fault = observableExpressionStates![observableExpression].Fault;
                            var argument = observableExpression.Argument;
                            ReleaseProjectionWithAccess(node);
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OwnOperationFault);
                                faultList.RemoveElementOccurrence(argument, elementComparer);
                            }
                        }
                        oldItems.Reverse();
                    }
                    var newItems = new ChangedElements<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                    {
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            if (ObserveElementWithAccess(element, e.NewStartingIndex + i) is { } fault)
                                (faultList ??= new FaultList(OwnOperationFault)).Add(new EvaluationFaultException(element, fault));
                            newItems.Add(positions.NodeAt(e.NewStartingIndex + i).Item.CommittedResult);
                        }
                    }
                    if (faultList is not null)
                        OperationFault = faultList.Fault;
                    if (oldItems.Count > 0)
                    {
                        eventArgs = newItems.Count > 0 ? newItems.ToReplacementOf(oldItems, e.OldStartingIndex) : oldItems.ToRemoval(e.OldStartingIndex);
                    }
                    else if (newItems.Count > 0)
                        eventArgs = newItems.ToAddition(e.NewStartingIndex);
                    if (eventArgs is not null && TryBeginEnumerationSnapshotPatchWithAccess(out var patched))
                    {
                        if (oldItems.Count > 0)
                            patched.RemoveRange(e.OldStartingIndex, oldItems.Count);
                        if (newItems.Count > 0)
                            newItems.InsertInto(patched, e.NewStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0 && e.OldStartingIndex != e.NewStartingIndex)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > positions.Count)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        var movedItems = new ChangedElements<TResult>();
                        var movedNode = positions.NodeAt(e.OldStartingIndex);
                        for (var i = 0; i < e.OldItems.Count && movedNode is not null; ++i)
                        {
                            movedItems.Add(movedNode.Item.CommittedResult);
                            movedNode = positions.Next(movedNode);
                        }
                        positions.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        eventArgs = movedItems.ToMove(e.NewStartingIndex, e.OldStartingIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var movedSnapshot))
                        {
                            movedSnapshot.RemoveRange(e.OldStartingIndex, movedItems.Count);
                            movedItems.InsertInto(movedSnapshot, e.NewStartingIndex);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    eventArgs = ResetWithAccess();
                    break;
            }
            if (eventArgs is not null)
                OnCollectionChanged(eventArgs);
        }
    }

    public override string ToString() =>
        $"mapping {source} with {Selector}";

    bool TryBeginEnumerationSnapshotPatchWithAccess([NotNullWhen(true)] out List<TResult>? snapshot)
    {
        if (enumerationSnapshot is { } existing && ++enumerationSnapshotPatches <= maximumEnumerationSnapshotPatches)
        {
            if (enumerationSnapshotShared)
            {
                if (Volatile.Read(ref liveEnumerations) > 0)
                {
                    existing = new List<TResult>(existing);
                    enumerationSnapshot = existing;
                }
                enumerationSnapshotShared = false;
            }
            snapshot = existing;
            return true;
        }
        InvalidateEnumerationSnapshotWithAccess();
        snapshot = null;
        return false;
    }

    private protected override bool TryCopyTo(TResult[] array, int arrayIndex)
    {
        lock (access)
        {
            if (array.Length - arrayIndex < positions.Count)
                return false;
            UseEnumerationSnapshotWithAccess().CopyTo(array, arrayIndex);
            return true;
        }
    }

    /// <summary>
    /// Gets the snapshot of the results kept for reading them in order, taking one where none is kept, and restarts the count of changes it may be patched for, since it is being read
    /// </summary>
    List<TResult> UseEnumerationSnapshotWithAccess()
    {
        if (enumerationSnapshot is null)
        {
            var results = new List<TResult>(positions.Count);
            for (var node = positions.FirstNode; node is not null; node = positions.Next(node))
                results.Add(node.Item.CommittedResult);
            enumerationSnapshot = results;
        }
        enumerationSnapshotPatches = 0;
        return enumerationSnapshot;
    }
}
