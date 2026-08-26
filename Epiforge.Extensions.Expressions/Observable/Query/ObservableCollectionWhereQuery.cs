namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionWhereQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, bool>> predicate) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    readonly object access = new();
    int count;
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly Dictionary<IObservableExpression<TElement, bool>, (Exception? fault, bool result)> evaluationsChanging = [];
    readonly Dictionary<IObservableExpression<TElement, bool>, int> observableExpressionCounts = [];
    readonly List<IObservableExpression<TElement, bool>> observableExpressions = [];
    internal readonly Expression<Func<TElement, bool>> Predicate = predicate;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
            {
                for (int i = 0, ii = observableExpressions.Count; i < ii; ++i)
                {
                    var observableExpression = observableExpressions[i];
                    if (!observableExpression.Evaluation.Result)
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
                foreach (var observableExpression in observableExpressionCounts.Keys)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    for (int i = 0, ii = observableExpressionCounts[observableExpression]; i < ii; ++i)
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
            return observableExpressions.Where(observableExpression => observableExpression.Evaluation.Result).Select(observableExpression => observableExpression.Argument).ToList().AsReadOnly().GetEnumerator();
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IObservableExpression<TElement, bool> observableExpression && e.PropertyName == nameof(IObservableExpression<,>.Evaluation))
            lock (access)
            {
                var (oldFault, oldResult) = evaluationsChanging![observableExpression];
                evaluationsChanging.Remove(observableExpression);
                var (newFault, newResult) = observableExpression.Evaluation;
                if (FaultList.ExchangeElementFault(OperationFault, observableExpression.Argument, elementComparer, oldFault, newFault, out var newOperationFault))
                    OperationFault = newOperationFault;
                if (oldResult != newResult)
                {
                    var action = newResult ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove;
                    var countIteration = newResult ? 1 : -1;
                    var translatedIndex = 0;
                    for (int i = 0, ii = observableExpressions.Count; i < ii; ++i)
                    {
                        var iObservableExpression = observableExpressions[i];
                        if (ReferenceEquals(iObservableExpression, observableExpression))
                        {
                            SetCount(count + countIteration);
                            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, iObservableExpression.Argument, translatedIndex));
                        }
                        if (iObservableExpression.Evaluation.Result)
                            ++translatedIndex;
                    }
                }
            }
    }

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (sender is IObservableExpression<TElement, bool> observableExpression && e.PropertyName == nameof(IObservableExpression<,>.Evaluation))
            lock (access)
                evaluationsChanging.Add(observableExpression, observableExpression.Evaluation);
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            var faultList = new FaultList();

            void processElement(TElement element)
            {
                var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                observableExpressions.Add(observableExpression);
                faultList!.Check(observableExpression);
                if (observableExpression.Evaluation.Result)
                    ++count;
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
                            var observableExpression = observableExpressions[e.OldStartingIndex + i];
                            observableExpressions.RemoveAt(e.OldStartingIndex + i);
                            var observableExpressionCount = observableExpressionCounts[observableExpression];
                            if (observableExpressionCount > 1)
                                observableExpressionCounts[observableExpression] = observableExpressionCount - 1;
                            else
                            {
                                observableExpressionCounts.Remove(observableExpression);
                                observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                            }
                            if (observableExpression.Evaluation.Fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.RemoveElement(observableExpression.Argument, elementComparer);
                            }
                            if (observableExpression.Evaluation.Result)
                                oldItems.Add(element);
                            observableExpression.Dispose();
                        }
                    var newItems = new List<TElement>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            var element = (TElement)e.NewItems[i]!;
                            var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                            observableExpressions.Insert(e.NewStartingIndex + i, observableExpression);
                            if (observableExpressionCounts.TryGetValue(observableExpression, out var existingCount))
                                observableExpressionCounts[observableExpression] = existingCount + 1;
                            else
                            {
                                observableExpressionCounts.Add(observableExpression, 1);
                                observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                                observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                            }
                            if (observableExpression.Evaluation.Fault is not null)
                            {
                                faultList ??= new FaultList(OperationFault);
                                faultList.Check(observableExpression);
                            }
                            if (observableExpression.Evaluation.Result)
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
                        var movedObservableExpressions = observableExpressions.GetRange(e.OldStartingIndex, e.OldItems.Count);
                        observableExpressions.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        observableExpressions.InsertRange(e.NewStartingIndex, movedObservableExpressions);
                        var newStartingIndex = TranslateIndex(e.NewStartingIndex);
                        if (oldStartingIndex != newStartingIndex)
                        {
                            var movedItems = movedObservableExpressions.Where(ae => ae.Evaluation.Result).Select(os => os.Argument).ToList().AsReadOnly();
                            if (movedItems.Count > 0)
                                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, newStartingIndex, oldStartingIndex);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    faultList = new FaultList();
                    foreach (var observableExpression in observableExpressionCounts.Keys)
                    {
                        observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                        observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                        for (int i = 0, ii = observableExpressionCounts[observableExpression]; i < ii; ++i)
                            observableExpression.Dispose();
                    }
                    observableExpressions.Clear();
                    observableExpressionCounts.Clear();
                    void processElement(TElement element)
                    {
                        var observableExpression = collectionObserver.ExpressionObserver.ObserveWithoutOptimization(Predicate, element);
                        observableExpressions.Add(observableExpression);
                        faultList!.Check(observableExpression);
                        if (observableExpression.Evaluation.Result)
                            ++newCount;
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
        index - observableExpressions.Take(index).Count(ae => !ae.Evaluation.Result);
}
