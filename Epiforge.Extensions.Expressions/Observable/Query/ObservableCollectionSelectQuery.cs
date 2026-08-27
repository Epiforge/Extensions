namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver)
{
    readonly object access = new();
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly Dictionary<IObservableExpression<TElement, TResult>, (int Positions, Exception? Fault)> observableExpressionStates = [];
    readonly List<(IObservableExpression<TElement, TResult> ObservableExpression, TResult CommittedResult)> projections = [];
    readonly EqualityComparer<TResult> resultComparer = EqualityComparer<TResult>.Default;
    internal readonly Expression<Func<TElement, TResult>> Selector = selector;

    public override TResult this[int index]
    {
        get
        {
            lock (access)
                return projections[index].CommittedResult;
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return projections.Count;
        }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                foreach (var observableExpression in observableExpressionStates.Keys)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = observableExpressionStates[observableExpression].Positions; i < ii; ++i)
                        observableExpression.Dispose();
                }
                source.CollectionChanged -= SourceCollectionChanged;
                source.PropertyChanging -= SourcePropertyChanging;
                source.PropertyChanged -= SourcePropertyChanged;
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
            var results = new List<TResult>(projections.Count);
            for (int i = 0, ii = projections.Count; i < ii; ++i)
                results.Add(projections[i].CommittedResult);
            return results.GetEnumerator();
        }
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IObservableExpression<TElement, TResult> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            if (!observableExpressionStates.TryGetValue(observableExpression, out var state))
                return;
            var (newFault, newResult) = observableExpression.Evaluation;
            if (FaultList.ExchangeElementFault(OperationFault, observableExpression.Argument, elementComparer, state.Fault, newFault, out var newOperationFault))
            {
                observableExpressionStates[observableExpression] = (state.Positions, newFault);
                OperationFault = newOperationFault;
            }
            for (int i = 0, ii = projections.Count; i < ii; ++i)
            {
                var (iObservableExpression, committedResult) = projections[i];
                if (ReferenceEquals(iObservableExpression, observableExpression) && !resultComparer.Equals(committedResult, newResult))
                {
                    projections[i] = (iObservableExpression, newResult);
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newResult, committedResult, i));
                }
            }
        }
    }

    IObservableExpression<TElement, TResult> ObserveElementWithAccess(TElement element, FaultList faultList)
    {
        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
        var (fault, result) = observableExpression.Evaluation;
        if (fault is not null)
            faultList.Add(new EvaluationFaultException(element, fault));
        if (observableExpressionStates.TryGetValue(observableExpression, out var state))
            observableExpressionStates[observableExpression] = (state.Positions + 1, state.Fault);
        else
        {
            observableExpressionStates.Add(observableExpression, (1, fault));
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        }
        return observableExpression;
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var faultList = new FaultList();

            void processElement(TElement element)
            {
                var observableExpression = ObserveElementWithAccess(element, faultList!);
                projections!.Add((observableExpression, observableExpression.Evaluation.Result));
            }

            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    processElement(source[i]);
            else
                foreach (var element in source)
                    processElement(element);

            OperationFault = faultList.Fault;

            source.CollectionChanged += SourceCollectionChanged;
            source.PropertyChanging += SourcePropertyChanging;
            source.PropertyChanged += SourcePropertyChanged;
        }
    }

    void ReleaseProjectionWithAccess((IObservableExpression<TElement, TResult> ObservableExpression, TResult CommittedResult) projection)
    {
        var observableExpression = projection.ObservableExpression;
        var state = observableExpressionStates[observableExpression];
        if (state.Positions > 1)
            observableExpressionStates[observableExpression] = (state.Positions - 1, state.Fault);
        else
        {
            observableExpressionStates.Remove(observableExpression);
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
        }
        observableExpression.Dispose();
    }

    NotifyCollectionChangedEventArgs ResetWithAccess()
    {
        foreach (var observableExpression in observableExpressionStates.Keys)
        {
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
            for (int i = 0, ii = observableExpressionStates[observableExpression].Positions; i < ii; ++i)
                observableExpression.Dispose();
        }

        observableExpressionStates.Clear();
        projections.Clear();

        var faultList = new FaultList();

        void processElement(TElement element)
        {
            var observableExpression = ObserveElementWithAccess(element, faultList!);
            projections!.Add((observableExpression, observableExpression.Evaluation.Result));
        }

        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                processElement(source[i]);
        else
            foreach (var element in source)
                processElement(element);

        OperationFault = faultList.Fault;

        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            NotifyCollectionChangedEventArgs? eventArgs = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new List<TResult>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                    {
                        List<(IObservableExpression<TElement, TResult> ObservableExpression, TResult CommittedResult)>? removedProjections = null;
                        try
                        {
                            removedProjections = projections.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        }
                        catch (ArgumentException)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        projections.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        for (int i = 0, ii = removedProjections.Count; i < ii; ++i)
                        {
                            var removedProjection = removedProjections[i];
                            oldItems.Add(removedProjection.CommittedResult);
                            ReleaseProjectionWithAccess(removedProjection);
                        }
                    }
                    var newItems = new List<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                    {
                        var faultList = new FaultList(OperationFault);
                        var addedProjections = new List<(IObservableExpression<TElement, TResult> ObservableExpression, TResult CommittedResult)>();
                        for (int i = 0, ii = e.NewItems.Count; i < ii; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            var addedObservableExpression = ObserveElementWithAccess(element, faultList);
                            var committedResult = addedObservableExpression.Evaluation.Result;
                            newItems.Add(committedResult);
                            addedProjections.Add((addedObservableExpression, committedResult));
                        }
                        projections.InsertRange(e.NewStartingIndex, addedProjections);
                        OperationFault = faultList.Fault;
                    }
                    if (oldItems.Count > 0)
                    {
                        if (newItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), e.OldStartingIndex);
                        else
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), e.OldStartingIndex);
                    }
                    else if (newItems.Count > 0)
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems?.Count > 0 && e.OldStartingIndex != e.NewStartingIndex)
                    {
                        List<(IObservableExpression<TElement, TResult> ObservableExpression, TResult CommittedResult)>? movedProjections = null;
                        try
                        {
                            movedProjections = projections.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        }
                        catch (ArgumentException)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        projections.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        projections.InsertRange(e.NewStartingIndex, movedProjections);
                        var movedItems = new List<TResult>(movedProjections.Count);
                        for (int i = 0, ii = movedProjections.Count; i < ii; ++i)
                            movedItems.Add(movedProjections[i].CommittedResult);
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems.AsReadOnly(), e.NewStartingIndex, e.OldStartingIndex);
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
}
