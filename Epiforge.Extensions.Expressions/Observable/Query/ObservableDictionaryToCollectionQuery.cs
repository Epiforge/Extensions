namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryToCollectionQuery<TElement, TKey, TValue> :
    ObservableCollectionQuery<TElement>
    where TKey : notnull
{
    public ObservableDictionaryToCollectionQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, Expression<Func<KeyValuePair<TKey, TValue>, TElement>> selector) :
        base(collectionObserver)
    {
        access = new();
        elementComparer = EqualityComparer<TElement>.Default;
        elements = new();
        evaluationsChanging = new();
        keyComparer = EqualityComparer<TKey>.Default;
        observableExpressions = new();
        this.source = source;
        Selector = selector;
    }

    readonly object access;
    readonly IEqualityComparer<TElement> elementComparer;
    readonly RangeObservableCollection<TElement> elements;
    readonly Dictionary<IObservableExpression<KeyValuePair<TKey, TValue>, TElement>, (Exception? fault, TElement result)> evaluationsChanging;
    readonly IEqualityComparer<TKey> keyComparer;
    readonly ObservableDictionary<TKey, IObservableExpression<KeyValuePair<TKey, TValue>, TElement>> observableExpressions;
    readonly ObservableDictionaryQuery<TKey, TValue> source;

    internal readonly Expression<Func<KeyValuePair<TKey, TValue>, TElement>> Selector;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
                return elements[index];
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return elements.Count;
        }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                foreach (var observableExpression in observableExpressions.Values)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                source.DictionaryChanged -= SourceDictionaryChanged;
                elements.CollectionChanged -= ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements).PropertyChanged -= ElementsPropertyChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
            foreach (var element in elements)
                yield return element;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IObservableExpression<KeyValuePair<TKey, TValue>, TElement> observableExpression && e.PropertyName == nameof(IObservableExpression<KeyValuePair<TKey, TValue>, TElement>.Evaluation))
            lock (access)
            {
                var (oldFault, oldElement) = evaluationsChanging![observableExpression];
                evaluationsChanging.Remove(observableExpression);
                var (newFault, newElement) = observableExpression.Evaluation;
                if (oldFault is not null && newFault is null)
                    elements.Add(newElement);
                else if (oldFault is null && newFault is not null)
                    elements.Remove(oldElement);
                else if (!elementComparer.Equals(oldElement, newElement))
                    elements[elements.IndexOf(oldElement)] = newElement;
                if (FaultList.ExchangeKeyFault(OperationFault, observableExpression.Argument.Key, keyComparer, oldFault, newFault, out var newOperationFault))
                    OperationFault = newOperationFault;
            }
    }

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (sender is IObservableExpression<KeyValuePair<TKey, TValue>, TElement> observableExpression && e.PropertyName == nameof(IObservableExpression<KeyValuePair<TKey, TValue>, TElement>.Evaluation))
            lock (access)
                evaluationsChanging.Add(observableExpression, observableExpression.Evaluation);
    }

    protected override void OnInitialization()
    {
        var faultList = new FaultList();
        var expressionObserver = collectionObserver.ExpressionObserver;
        foreach (var keyValuePair in source)
        {
            var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
            if (!faultList.Check(observableExpression))
                elements.Add(observableExpression.Evaluation.Result);
            observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            observableExpressions.Add(keyValuePair.Key, observableExpression);
        }
        OperationFault = faultList.Fault;
        source.DictionaryChanged += SourceDictionaryChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        lock (access)
        {
            var expressionObserver = collectionObserver.ExpressionObserver;
            if (e.Action is NotifyDictionaryChangedAction.Reset)
            {
                foreach (var observableExpression in observableExpressions.Values)
                {
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();
                evaluationsChanging.Clear();
                var newElements = new List<TElement>();
                var faultList = new FaultList();
                foreach (var keyValuePair in source)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
                    if (!faultList.Check(observableExpression))
                        newElements.Add(observableExpression.Evaluation.Result);
                    observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(keyValuePair.Key, observableExpression);
                }
                elements.Reset(newElements);
                OperationFault = faultList.Fault;
            }
            else
            {
                FaultList? faultList = null;
                foreach (var keyValuePair in e.OldItems)
                {
                    var observableExpression = observableExpressions[keyValuePair.Key];
                    var (fault, element) = observableExpression.Evaluation;
                    if (fault is not null)
                    {
                        faultList ??= new FaultList(OperationFault);
                        faultList.RemoveKey(observableExpression.Argument.Key, keyComparer);
                    }
                    else
                        elements.Remove(element);
                    observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                    observableExpressions.Remove(keyValuePair.Key);
                }
                foreach (var keyValuePair in e.NewItems)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
                    var (fault, element) = observableExpression.Evaluation;
                    if (fault is not null)
                    {
                        faultList ??= new FaultList(OperationFault);
                        faultList.Check(observableExpression);
                    }
                    else
                        elements.Add(element);
                    observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(keyValuePair.Key, observableExpression);
                }
                if (faultList is not null)
                    OperationFault = faultList.Fault;
            }
        }
    }
}
