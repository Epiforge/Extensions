namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryToCollectionQuery<TElement, TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, Expression<Func<KeyValuePair<TKey, TValue>, TElement>> selector) :
    ObservableCollectionQuery<TElement>(collectionObserver)
    where TKey : notnull
{
    readonly object access = new();
    readonly EqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    readonly ObservableRangeCollection<TElement> elements = [];
    readonly List<TKey> keysByPosition = [];
    readonly Dictionary<TKey, (IObservableExpression<KeyValuePair<TKey, TValue>, TElement> ObservableExpression, Exception? CommittedFault, TElement CommittedElement)> observableExpressions = new(source.KeyComparer);
    readonly Dictionary<TKey, int> positionsByKey = new(source.KeyComparer);
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

    void AppendElementWithAccess(TKey key, TElement element, IList<TElement> into)
    {
        positionsByKey.Add(key, keysByPosition.Count);
        keysByPosition.Add(key);
        into.Add(element);
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
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (sender is not IObservableExpression<KeyValuePair<TKey, TValue>, TElement> observableExpression || e.PropertyName != nameof(IObservableExpression<,>.Evaluation))
            return;
        lock (access)
        {
            var key = observableExpression.Argument.Key;
            if (!observableExpressions.TryGetValue(key, out var committed) || !ReferenceEquals(committed.ObservableExpression, observableExpression))
                return;
            var (newFault, newElement) = observableExpression.Evaluation;
            if (committed.CommittedFault is not null && newFault is null)
                AppendElementWithAccess(key, newElement, elements);
            else if (committed.CommittedFault is null && newFault is not null)
                RemoveElementWithAccess(key);
            else if (newFault is null && !elementComparer.Equals(committed.CommittedElement, newElement))
                ReplaceElementWithAccess(key, newElement);
            observableExpressions[key] = (observableExpression, newFault, newElement);
            if (FaultList.ExchangeKeyFault(OperationFault, key, source.KeyComparer, committed.CommittedFault, newFault, out var newOperationFault))
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
                AppendElementWithAccess(keyValuePair.Key, element, elements);
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, element));
        }
        OperationFault = faultList.Fault;
        source.DictionaryChanged += SourceDictionaryChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
    }

    void RemoveElementWithAccess(TKey key)
    {
        if (!positionsByKey.TryGetValue(key, out var position))
            return;
        positionsByKey.Remove(key);
        keysByPosition.RemoveAt(position);
        elements.RemoveAt(position);
        for (int i = position, ii = keysByPosition.Count; i < ii; ++i)
            positionsByKey[keysByPosition[i]] = i;
    }

    void ReplaceElementWithAccess(TKey key, TElement element)
    {
        if (positionsByKey.TryGetValue(key, out var position))
            elements[position] = element;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
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
                keysByPosition.Clear();
                positionsByKey.Clear();
                var newElements = new List<TElement>();
                var faultList = new FaultList();
                foreach (var keyValuePair in source)
                {
                    var observableExpression = expressionObserver.ObserveWithoutOptimization(Selector, keyValuePair);
                    var (fault, element) = observableExpression.Evaluation;
                    if (!faultList.Check(observableExpression))
                        AppendElementWithAccess(keyValuePair.Key, element, newElements);
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
                        faultList.RemoveKey(keyValuePair.Key, source.KeyComparer);
                    }
                    else
                        RemoveElementWithAccess(keyValuePair.Key);
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
                        AppendElementWithAccess(keyValuePair.Key, element, elements);
                    observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
                    observableExpressions.Add(keyValuePair.Key, (observableExpression, fault, element));
                }
                if (faultList is not null)
                    OperationFault = faultList.Fault;
            }
        }
    }
}
