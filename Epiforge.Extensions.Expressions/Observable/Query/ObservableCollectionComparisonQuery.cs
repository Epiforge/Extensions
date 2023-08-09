namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionComparisonQuery<TResult> :
    ObservableScalarQuery<TResult>
{
    public ObservableCollectionComparisonQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TResult> observableCollectionQuery, int soughtComparison) :
        base(collectionObserver)
    {
        access = new();
        this.observableCollectionQuery = observableCollectionQuery;
        SoughtComparison = soughtComparison;
        comparer = Comparer<TResult>.Default;
    }

    readonly object access;
    readonly IComparer<TResult> comparer;
    readonly ObservableCollectionQuery<TResult> observableCollectionQuery;

    internal readonly int SoughtComparison;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        lock (access)
        {
            if (observableCollectionQuery.OperationFault is { } fault)
                Evaluation = (fault, default!);
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
        if (e.PropertyName == nameof(ObservableCollectionQuery<TResult>.OperationFault))
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
}
