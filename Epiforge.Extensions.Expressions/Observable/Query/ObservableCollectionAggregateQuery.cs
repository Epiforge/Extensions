namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) :
    ObservableCollectionScalarQuery<TElement, TResult>(collectionObserver, observableCollectionQuery)
{
    internal readonly Func<TAccumulate, TElement, TAccumulate> Func = func;
    internal readonly Func<TAccumulate, TResult> ResultSelector = resultSelector;
    internal readonly Func<TAccumulate> SeedFactory = seedFactory;

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
        if (observableCollectionQuery.OperationFault is { } fault)
            Evaluation = (fault, default!);
        else
        {
            try
            {
                Evaluation = (null, Enumerable.Aggregate(observableCollectionQuery, SeedFactory(), Func, ResultSelector));
            }
            catch (Exception ex)
            {
                Evaluation = (ex, default!);
            }
        }
    }

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"aggregate of {observableCollectionQuery}";
}
