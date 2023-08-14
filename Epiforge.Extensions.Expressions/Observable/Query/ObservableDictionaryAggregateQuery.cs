namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryAggregateQuery<TKey, TValue, TAccumulate, TResult> :
    ObservableDictionaryScalarQuery<TKey, TValue, TResult>
    where TKey : notnull
{
    public ObservableDictionaryAggregateQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery, Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) :
        base(collectionObserver, observableDictionaryQuery)
    {
        SeedFactory = seedFactory;
        Func = func;
        ResultSelector = resultSelector;
    }

    internal readonly Func<TAccumulate, TKey, TValue, TAccumulate> Func;
    internal readonly Func<TAccumulate, TResult> ResultSelector;
    internal readonly Func<TAccumulate> SeedFactory;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    TAccumulate AggregateFunc(TAccumulate a, KeyValuePair<TKey, TValue> kv) =>
        Func(a, kv.Key, kv.Value);

    void Evaluate()
    {
        if (observableDictionaryQuery.OperationFault is { } fault)
            Evaluation = (fault, default!);
        else
        {
            try
            {
                Evaluation = (null, Enumerable.Aggregate(observableDictionaryQuery, SeedFactory(), AggregateFunc, ResultSelector));
            }
            catch (Exception ex)
            {
                Evaluation = (ex, default!);
            }
        }
    }

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        Evaluate();

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableDictionaryQuery<TKey, TValue>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"aggregate of {observableDictionaryQuery}";
}
