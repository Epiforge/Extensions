namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAggregateQuery<TElement, TAccumulate, TResult> :
    ObservableCollectionScalarQuery<TElement, TResult>
{
    public ObservableCollectionAggregateQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) :
        base(collectionObserver, observableCollectionQuery)
    {
        SeedFactory = seedFactory;
        Func = func;
        ResultSelector = resultSelector;
    }

    internal readonly Func<TAccumulate, TElement, TAccumulate> Func;
    internal readonly Func<TAccumulate, TResult> ResultSelector;
    internal readonly Func<TAccumulate> SeedFactory;

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
        if (e.PropertyName == nameof(ObservableCollectionQuery<TElement>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        Evaluate();
    }
}
