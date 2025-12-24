namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionQuery<TResult>(collectionObserver)
{
    readonly object access = new();
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly Dictionary<IObservableExpression<TElement, TResult>, (Exception? fault, TResult result)> evaluationsChanging = [];
    readonly Dictionary<IObservableExpression<TElement, TResult>, int> observableExpressionCounts = [];
    readonly List<IObservableExpression<TElement, TResult>> observableExpressions = [];
    readonly EqualityComparer<TResult> resultComparer = EqualityComparer<TResult>.Default;
    internal readonly Expression<Func<TElement, TResult>> Selector = selector;

    public override TResult this[int index]
    {
        get
        {
            lock (access)
                return observableExpressions[index].Evaluation.Result;
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return observableExpressions.Count;
        }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                foreach (var observableExpression in observableExpressionCounts.Keys)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = observableExpressionCounts[observableExpression]; i < ii; ++i)
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
            foreach (var observableExpression in observableExpressions)
                yield return observableExpression.Evaluation.Result;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IObservableExpression<TElement, TResult> observableExpression && e.PropertyName == nameof(IObservableExpression<,>.Evaluation))
            lock (access)
            {
                var (oldFault, oldResult) = evaluationsChanging![observableExpression];
                evaluationsChanging.Remove(observableExpression);
                var (newFault, newResult) = observableExpression.Evaluation;
                if (FaultList.ExchangeElementFault(OperationFault, observableExpression.Argument, elementComparer, oldFault, newFault, out var newOperationFault))
                    OperationFault = newOperationFault;
                if (!resultComparer.Equals(oldResult, newResult))
                    for (int i = 0, ii = observableExpressions.Count; i < ii; ++i)
                        if (ReferenceEquals(observableExpressions[i], observableExpression))
                            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newResult, oldResult, i));
            }
    }

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (sender is IObservableExpression<TElement, TResult> observableExpression && e.PropertyName == nameof(IObservableExpression<,>.Evaluation))
            lock (access)
                evaluationsChanging.Add(observableExpression, observableExpression.Evaluation);
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var evaluationFaultExceptions = new List<EvaluationFaultException>();

            void processElement(TElement element)
            {
                var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
                observableExpressions.Add(observableExpression);
                if (observableExpression.Evaluation.Fault is { } fault)
                    evaluationFaultExceptions!.Add(new EvaluationFaultException(element, fault));
                if (observableExpressionCounts.TryGetValue(observableExpression, out var observableExpressionCount))
                    observableExpressionCounts[observableExpression] = observableExpressionCount + 1;
                else
                {
                    observableExpressionCounts.Add(observableExpression, 1);
                    observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                }
            }

            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    processElement(source[i]);
            else
                foreach (var element in source)
                    processElement(element);

            if (evaluationFaultExceptions.Count > 0)
                OperationFault = new AggregateException(evaluationFaultExceptions);

            source.CollectionChanged += SourceCollectionChanged;
            source.PropertyChanging += SourcePropertyChanging;
            source.PropertyChanged += SourcePropertyChanged;
        }
    }

    NotifyCollectionChangedEventArgs ResetWithAccess()
    {
        foreach (var observableExpression in observableExpressionCounts.Keys)
        {
            observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
            for (int i = 0, ii = observableExpressionCounts[observableExpression]; i < ii; ++i)
                observableExpression.Dispose();
        }

        evaluationsChanging.Clear();
        observableExpressionCounts.Clear();
        observableExpressions.Clear();

        var evaluationFaultExceptions = new List<EvaluationFaultException>();

        void processElement(TElement element)
        {
            var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
            observableExpressions.Add(observableExpression);
            if (observableExpression.Evaluation.Fault is { } fault)
                evaluationFaultExceptions!.Add(new EvaluationFaultException(element, fault));
            if (observableExpressionCounts.TryGetValue(observableExpression, out var observableExpressionCount))
                observableExpressionCounts[observableExpression] = observableExpressionCount + 1;
            else
            {
                observableExpressionCounts.Add(observableExpression, 1);
                observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            }
        }

        if (!source.HasIndexerPenalty)
            for (int i = 0, ii = source.Count; i < ii; ++i)
                processElement(source[i]);
        else
            foreach (var element in source)
                processElement(element);

        OperationFault = evaluationFaultExceptions.Count == 0 ? null : new AggregateException(evaluationFaultExceptions);

        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }

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
                        List<IObservableExpression<TElement, TResult>>? removedObservableExpressions = null;
                        try
                        {
                            removedObservableExpressions = observableExpressions.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        }
                        catch (ArgumentException)
                        {
                            var previousCount = observableExpressions.Count;
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        observableExpressions.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        for (int i = 0, ii = removedObservableExpressions.Count; i < ii; ++i)
                        {
                            var removedObservableExpression = removedObservableExpressions[i];
                            oldItems.Add(removedObservableExpression.Evaluation.Result);
                            var observableExpressionCount = observableExpressionCounts[removedObservableExpression];
                            if (observableExpressionCount == 1)
                            {
                                removedObservableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                                removedObservableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                                observableExpressionCounts.Remove(removedObservableExpression);
                            }
                            else
                                observableExpressionCounts[removedObservableExpression] = observableExpressionCount - 1;
                            removedObservableExpression.Dispose();
                        }
                    }
                    var newItems = new List<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                    {
                        var addedObservableExpressions = new List<IObservableExpression<TElement, TResult>>();
                        for (int i = 0, ii = e.NewItems.Count; i < ii; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            var addedObservableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Selector, element);
                            newItems.Add(addedObservableExpression.Evaluation.Result);
                            addedObservableExpressions.Add(addedObservableExpression);
                            if (observableExpressionCounts.TryGetValue(addedObservableExpression, out var observableExpressionCount))
                                observableExpressionCounts[addedObservableExpression] = observableExpressionCount + 1;
                            else
                            {
                                observableExpressionCounts.Add(addedObservableExpression, 1);
                                addedObservableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                                addedObservableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                            }
                        }
                        observableExpressions.InsertRange(e.NewStartingIndex, addedObservableExpressions);
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
                        List<IObservableExpression<TElement, TResult>>? movedObservableExpressions = null;
                        try
                        {
                            movedObservableExpressions = observableExpressions.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        }
                        catch (ArgumentException)
                        {
                            var previousCount = observableExpressions.Count;
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        observableExpressions.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        observableExpressions.InsertRange(e.NewStartingIndex, movedObservableExpressions);
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedObservableExpressions.Select(oe => oe.Evaluation.Result).ToList().AsReadOnly(), e.NewStartingIndex, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    {
                        var previousCount = observableExpressions.Count;
                        eventArgs = ResetWithAccess();
                    }
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
