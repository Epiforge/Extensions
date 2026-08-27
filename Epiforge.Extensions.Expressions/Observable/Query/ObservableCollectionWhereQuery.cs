namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionWhereQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, bool>> predicate) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    readonly object access = new();
    int count;
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly List<(IObservableExpression<TElement, bool> ObservableExpression, bool IsIncluded)> memberships = [];
    readonly Dictionary<IObservableExpression<TElement, bool>, (int Positions, Exception? Fault)> observableExpressionStates = [];
    internal readonly Expression<Func<TElement, bool>> Predicate = predicate;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
            {
                for (int i = 0, ii = memberships.Count; i < ii; ++i)
                {
                    var (observableExpression, isIncluded) = memberships[i];
                    if (!isIncluded)
                        continue;
                    if (--index == -1)
                        return observableExpression.Argument;
                }
                throw ExceptionHelper.IndexArgumentWasOutOfRange;
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
                foreach (var observableExpression in observableExpressionStates.Keys)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = observableExpressionStates[observableExpression].Positions; i < ii; ++i)
                        observableExpression.Dispose();
                }
                source.CollectionChanged -= SourceCollectionChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
        {
            var elements = new List<TElement>(count);
            for (int i = 0, ii = memberships.Count; i < ii; ++i)
            {
                var (observableExpression, isIncluded) = memberships[i];
                if (isIncluded)
                    elements.Add(observableExpression.Argument);
            }
            return elements.GetEnumerator();
        }
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IObservableExpression<TElement, bool> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
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
            var translatedIndex = 0;
            for (int i = 0, ii = memberships.Count; i < ii; ++i)
            {
                var (iObservableExpression, isIncluded) = memberships[i];
                if (isIncluded != newResult && ReferenceEquals(iObservableExpression, observableExpression))
                {
                    memberships[i] = (iObservableExpression, newResult);
                    isIncluded = newResult;
                    SetCount(count + (newResult ? 1 : -1));
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(newResult ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove, iObservableExpression.Argument, translatedIndex));
                }
                if (isIncluded)
                    ++translatedIndex;
            }
        }
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var faultList = new FaultList();

            void processElement(TElement element)
            {
                var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                var (fault, result) = observableExpression.Evaluation;
                memberships!.Add((observableExpression, result));
                faultList!.Check(observableExpression);
                if (result)
                    ++count;
                if (observableExpressionStates!.TryGetValue(observableExpression, out var state))
                    observableExpressionStates[observableExpression] = (state.Positions + 1, state.Fault);
                else
                {
                    observableExpressionStates.Add(observableExpression, (1, fault));
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                }
            }

            if (!source.HasIndexerPenalty)
                for (int i = 0, ii = source.Count; i < ii; ++i)
                    processElement(source[i]);
            else
                foreach (var element in source)
                    processElement(element);

            OperationFault = faultList.Fault;

            source.CollectionChanged += SourceCollectionChanged;
        }
    }

    void SetCount(int value) =>
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity", Justification = @"Splitting this up into more methods is ¯\_(ツ)_/¯")]
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
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
                            var (observableExpression, isIncluded) = memberships[e.OldStartingIndex + i];
                            memberships.RemoveAt(e.OldStartingIndex + i);
                            var state = observableExpressionStates[observableExpression];
                            if (state.Positions > 1)
                                observableExpressionStates[observableExpression] = (state.Positions - 1, state.Fault);
                            else
                            {
                                observableExpressionStates.Remove(observableExpression);
                                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                            }
                            if (state.Fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.RemoveElement(observableExpression.Argument, elementComparer);
                            }
                            if (isIncluded)
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
                            memberships.Insert(e.NewStartingIndex + i, (observableExpression, result));
                            if (observableExpressionStates.TryGetValue(observableExpression, out var state))
                                observableExpressionStates[observableExpression] = (state.Positions + 1, state.Fault);
                            else
                            {
                                observableExpressionStates.Add(observableExpression, (1, fault));
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
                        if (oldItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), TranslateIndex(e.NewStartingIndex));
                        else
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), TranslateIndex(e.NewStartingIndex));
                    }
                    else if (oldItems.Count > 0)
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), TranslateIndex(e.OldStartingIndex));
                    newCount = count + newItems.Count - oldItems.Count;
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0)
                    {
                        var oldStartingIndex = TranslateIndex(e.OldStartingIndex);
                        var movedMemberships = memberships.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        memberships.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        memberships.InsertRange(e.NewStartingIndex, movedMemberships);
                        var newStartingIndex = TranslateIndex(e.NewStartingIndex);
                        if (oldStartingIndex != newStartingIndex)
                        {
                            var movedItems = movedMemberships.Where(membership => membership.IsIncluded).Select(membership => membership.ObservableExpression.Argument).ToList().AsReadOnly();
                            if (movedItems.Count > 0)
                                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, newStartingIndex, oldStartingIndex);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    faultList = new FaultList();
                    foreach (var observableExpression in observableExpressionStates.Keys)
                    {
                        observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                        for (int i = 0, ii = observableExpressionStates[observableExpression].Positions; i < ii; ++i)
                            observableExpression.Dispose();
                    }
                    memberships.Clear();
                    observableExpressionStates.Clear();
                    void processElement(TElement element)
                    {
                        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                        var (fault, result) = observableExpression.Evaluation;
                        memberships!.Add((observableExpression, result));
                        faultList!.Check(observableExpression);
                        if (result)
                            ++newCount;
                        if (observableExpressionStates!.TryGetValue(observableExpression, out var state))
                            observableExpressionStates[observableExpression] = (state.Positions + 1, state.Fault);
                        else
                        {
                            observableExpressionStates.Add(observableExpression, (1, fault));
                            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                        }
                    }
                    if (!source.HasIndexerPenalty)
                        for (int i = 0, ii = source.Count; i < ii; ++i)
                            processElement(source[i]);
                    else
                        foreach (var element in source)
                            processElement(element);
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

    int TranslateIndex(int index) =>
        index - memberships.Take(index).Count(membership => !membership.IsIncluded);
}
