namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver)
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
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    List<TResult>? enumerationSnapshot;
    int enumerationSnapshotPatches;
    bool enumerationSnapshotShared;
    int liveEnumerations;
    readonly Dictionary<IObservableExpression<TElement, TResult>, (Projection Projection, List<PrefixWeightedSequenceNode<Projection>> Nodes, Exception? Fault)> observableExpressionStates = [];
    readonly PrefixWeightedSequence<Projection> positions = new();
    readonly EqualityComparer<TResult> resultComparer = EqualityComparer<TResult>.Default;
    internal readonly Expression<Func<TElement, TResult>> Selector = selector;

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
                return positions.NodeAt(index).Item.CommittedResult;
            }
        }
    }

    public override int Count
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
                foreach (var (observableExpression, state) in observableExpressionStates)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                        observableExpression.Dispose();
                }
                source.CollectionChanged -= SourceCollectionChanged;
                source.PropertyChanging -= SourcePropertyChanging;
                source.PropertyChanged -= SourcePropertyChanged;
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

    public override IEnumerator<TResult> GetEnumerator()
    {
        lock (access)
        {
            if (enumerationSnapshot is null)
            {
                var results = new List<TResult>(positions.Count);
                for (var node = positions.FirstNode; node is not null; node = positions.Next(node))
                    results.Add(node.Item.CommittedResult);
                enumerationSnapshot = results;
            }
            enumerationSnapshotPatches = 0;
            enumerationSnapshotShared = true;
            Interlocked.Increment(ref liveEnumerations);
            return new SnapshotEnumerator(this, enumerationSnapshot);
        }
    }

    void InvalidateEnumerationSnapshotWithAccess()
    {
        enumerationSnapshot = null;
        enumerationSnapshotShared = false;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<TElement, TResult> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (!observableExpressionStates.TryGetValue(observableExpression, out var state))
                return;
            var (newFault, newResult) = observableExpression.Evaluation;
            if (FaultList.ExchangeElementFault(OperationFault, observableExpression.Argument, elementComparer, state.Fault, newFault, out var newOperationFault))
            {
                observableExpressionStates[observableExpression] = (state.Projection, state.Nodes, newFault);
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

    void ObserveElementWithAccess(TElement element, int index, FaultList faultList)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
        var (fault, result) = observableExpression.Evaluation;
        if (fault is not null)
            faultList.Add(new EvaluationFaultException(element, fault));
        if (observableExpressionStates.TryGetValue(observableExpression, out var state))
            state.Nodes.Add(positions.Insert(index, state.Projection, 1));
        else
        {
            var projection = new Projection(observableExpression, result);
            observableExpressionStates.Add(observableExpression, (projection, [positions.Insert(index, projection, 1)], fault));
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        }
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var faultList = new FaultList();
            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    ObserveElementWithAccess(source[i], positions.Count, faultList);
            else
                foreach (var element in source)
                    ObserveElementWithAccess(element, positions.Count, faultList);
            OperationFault = faultList.Fault;
            source.CollectionChanged += SourceCollectionChanged;
            source.PropertyChanging += SourcePropertyChanging;
            source.PropertyChanged += SourcePropertyChanged;
        }
    }

    void ReleaseProjectionWithAccess(PrefixWeightedSequenceNode<Projection> node)
    {
        var observableExpression = node.Item.ObservableExpression;
        var state = observableExpressionStates[observableExpression];
        state.Nodes.Remove(node);
        if (state.Nodes.Count == 0)
        {
            observableExpressionStates.Remove(observableExpression);
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
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
        foreach (var (observableExpression, state) in observableExpressionStates)
        {
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
            for (int i = 0, ii = state.Nodes.Count; i < ii; ++i)
                observableExpression.Dispose();
        }
        observableExpressionStates.Clear();
        positions.Clear();
        InvalidateEnumerationSnapshotWithAccess();
        var faultList = new FaultList();
        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                ObserveElementWithAccess(source[i], positions.Count, faultList);
        else
            foreach (var element in source)
                ObserveElementWithAccess(element, positions.Count, faultList);
        OperationFault = faultList.Fault;
        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            FaultList? faultList = null;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new List<TResult>();
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
                            oldItems.Insert(0, node.Item.CommittedResult);
                            var observableExpression = node.Item.ObservableExpression;
                            var fault = observableExpressionStates[observableExpression].Fault;
                            var argument = observableExpression.Argument;
                            ReleaseProjectionWithAccess(node);
                            if (fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.RemoveElementOccurrence(argument, elementComparer);
                            }
                        }
                    }
                    var newItems = new List<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                    {
                        faultList ??= new FaultList(OperationFault);
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            ObserveElementWithAccess((TElement)e.NewItems[i]!, e.NewStartingIndex + i, faultList);
                            newItems.Add(positions.NodeAt(e.NewStartingIndex + i).Item.CommittedResult);
                        }
                    }
                    if (faultList is not null)
                        OperationFault = faultList.Fault;
                    if (oldItems.Count > 0)
                    {
                        if (newItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), e.OldStartingIndex);
                        else
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), e.OldStartingIndex);
                    }
                    else if (newItems.Count > 0)
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), e.NewStartingIndex);
                    if (eventArgs is not null && TryBeginEnumerationSnapshotPatchWithAccess(out var patched))
                    {
                        if (oldItems.Count > 0)
                            patched.RemoveRange(e.OldStartingIndex, oldItems.Count);
                        if (newItems.Count > 0)
                            patched.InsertRange(e.NewStartingIndex, newItems);
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
                        var movedItems = new List<TResult>(e.OldItems.Count);
                        var movedNode = positions.NodeAt(e.OldStartingIndex);
                        for (var i = 0; i < e.OldItems.Count && movedNode is not null; ++i)
                        {
                            movedItems.Add(movedNode.Item.CommittedResult);
                            movedNode = positions.Next(movedNode);
                        }
                        positions.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems.AsReadOnly(), e.NewStartingIndex, e.OldStartingIndex);
                        if (TryBeginEnumerationSnapshotPatchWithAccess(out var movedSnapshot))
                        {
                            movedSnapshot.RemoveRange(e.OldStartingIndex, movedItems.Count);
                            movedSnapshot.InsertRange(e.NewStartingIndex, movedItems);
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

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Count))
            OnPropertyChanged(e);
    }

    void SourcePropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(Count))
            OnPropertyChanging(e);
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
}
