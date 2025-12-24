namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionComparisonQuery<TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TResult> observableCollectionQuery, int soughtComparison) :
    ObservableScalarQuery<TResult>(collectionObserver)
{
#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    readonly Comparer<TResult> comparer = Comparer<TResult>.Default;
    internal readonly int SoughtComparison = soughtComparison;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        lock (access)
        {
            if (observableCollectionQuery.OperationFault is { } queryFault)
                Evaluation = (queryFault, default!);
            else if (observableCollectionQuery.HasIndexerPenalty)
            {
                using var enumerator = observableCollectionQuery.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current;
                    while (enumerator.MoveNext())
                    {
                        var enumeratorValue = enumerator.Current;
                        if (comparer.Compare(enumeratorValue, value) == SoughtComparison)
                            value = enumeratorValue;
                    }
                    Evaluation = (null, value);
                }
                else
                    Evaluation = (ExceptionHelper.SequenceContainsNoElements, default!);
            }
            else
            {
                if (observableCollectionQuery.Count > 0)
                {
                    var value = observableCollectionQuery[0];
                    for (var i = 1; i < observableCollectionQuery.Count; i++)
                    {
                        var indexerValue = observableCollectionQuery[i];
                        if (comparer.Compare(indexerValue, value) == SoughtComparison)
                            value = indexerValue;
                    }
                    Evaluation = (null, value);
                }
                else
                    Evaluation = (ExceptionHelper.SequenceContainsNoElements, default!);
            }
        }
    }

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (observableCollectionQuery.OperationFault is null)
        {
            if (Evaluation.Fault is not null || e.Action is NotifyCollectionChangedAction.Reset)
                Evaluate();
            else if (e.Action is not NotifyCollectionChangedAction.Move)
            {
                var value = Evaluation.Result;
                if (e.OldItems is { } oldItems && oldItems.Cast<TResult>().Any(oldItem => comparer.Compare(value, oldItem) == 0))
                {
                    if (e.NewItems is { } replacingItems)
                    {
                        var valueScan = value;
                        foreach (var newValue in replacingItems.Cast<TResult>())
                            if (comparer.Compare(newValue, valueScan) == SoughtComparison)
                                valueScan = newValue;
                        if (comparer.Compare(valueScan, value) != 0)
                        {
                            Evaluation = (null, valueScan);
                            return;
                        }
                    }
                    Evaluate();
                }
                else if (e.NewItems is { } newItems)
                {
                    var valueScan = value;
                    foreach (var newValue in newItems.Cast<TResult>())
                        if (comparer.Compare(newValue, valueScan) == SoughtComparison)
                            valueScan = newValue;
                    if (comparer.Compare(valueScan, value) != 0)
                        Evaluation = (null, valueScan);
                }
            }
        }
    }

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.OperationFault))
        {
            if (observableCollectionQuery.OperationFault is { } fault)
                Evaluation = (fault, default!);
            else
                Evaluate();
        }
    }

    protected override void OnInitialization()
    {
        Evaluate();
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
    }

    public override string ToString() =>
        $"{(SoughtComparison > 0 ? "max" : "min")} of {observableCollectionQuery}";
}
