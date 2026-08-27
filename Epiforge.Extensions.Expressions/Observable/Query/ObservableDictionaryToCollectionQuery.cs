namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, Expression<Func<KeyValuePair<TKey, TValue>, TElement>> selector) :
    ObservableCollectionQuery<TElement>(collectionObserver)
    where TKey : notnull
{
    readonly object access = new();
    readonly EqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly ObservableRangeCollection<TElement> elements = [];
    readonly IEqualityComparer<TKey> keyComparer = EqualityComparer<TKey>.Default;
    readonly ObservableDictionary<TKey, (IObservableExpression<KeyValuePair<TKey, TValue>, TElement> ObservableExpression, Exception? CommittedFault, TElement CommittedElement)> observableExpressions = [];
    internal readonly Expression<Func<KeyValuePair<TKey, TValue>, TElement>> Selector = selector;

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
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
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
        {
            var snapshot = new List<TElement>(elements.Count);
            for (int i = 0, ii = elements.Count; i < ii; ++i)
                snapshot.Add(elements[i]);
            return snapshot.GetEnumerator();
        }
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IObservableExpression<KeyValuePair<TKey, TValue>, TElement> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            var key = observableExpression.Argument.Key;
            if (!observableExpressions.TryGetValue(key, out var committed) || !ReferenceEquals(committed.ObservableExpression, observableExpression))
                return;
            var (newFault, newElement) = observableExpression.Evaluation;
            if (committed.CommittedFault is not null && newFault is null)
                elements.Add(newElement);
            else if (committed.CommittedFault is null && newFault is not null)
                elements.Remove(committed.CommittedElement);
            else if (newFault is null && !elementComparer.Equals(committed.CommittedElement, newElement))
                elements[elements.IndexOf(committed.CommittedElement)] = newElement;
            observableExpressions[key] = (observableExpression, newFault, newElement);
            if (FaultList.ExchangeKeyFault(OperationFault, key, keyComparer, committed.CommittedFault, newFault, out var newOperationFault))
                OperationFault = newOperationFault;
        }
    }

    protected override void OnInitialization()
    {
        var faultList = new FaultList();
        var expressionObserver = collectionObserver.ExpressionObserver;
        foreach (var keyValuePair in source)
        {
            var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
            var (fault, element) = observableExpression.Evaluation;
            if (!faultList.Check(observableExpression))
                elements.Add(element);
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, element));
        }
        OperationFault = faultList.Fault;
        source.DictionaryChanged += SourceDictionaryChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        lock (access)
        {
            var expressionObserver = collectionObserver.ExpressionObserver;
            if (e.Action is NotifyDictionaryChangedAction.Reset)
            {
                foreach (var (observableExpression, _, _) in observableExpressions.Values)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                observableExpressions.Clear();
                var newElements = new List<TElement>();
                var faultList = new FaultList();
                foreach (var keyValuePair in source)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
                    var (fault, element) = observableExpression.Evaluation;
                    if (!faultList.Check(observableExpression))
                        newElements.Add(element);
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, element));
                }
                elements.Reset(newElements);
                OperationFault = faultList.Fault;
            }
            else
            {
                FaultList? faultList = null;
                foreach (var keyValuePair in e.OldItems)
                {
                    if (!observableExpressions.TryGetValue(keyValuePair.Key, out var committed))
                        continue;
                    if (committed.CommittedFault is not null)
                    {
                        faultList ??= new FaultList(OperationFault);
                        faultList.RemoveKey(keyValuePair.Key, keyComparer);
                    }
                    else
                        elements.Remove(committed.CommittedElement);
                    committed.ObservableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    committed.ObservableExpression.Dispose();
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
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, element));
                }
                if (faultList is not null)
                    OperationFault = faultList.Fault;
            }
        }
    }
}
