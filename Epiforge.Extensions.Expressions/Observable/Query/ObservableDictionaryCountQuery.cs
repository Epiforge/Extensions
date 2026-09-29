namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryCountQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery) :
    ObservableDictionaryScalarQuery<TKey, TValue, int>(collectionObserver, observableDictionaryQuery)
    where TKey : notnull
{
    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableDictionaryQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableDictionaryQuery.DictionaryChanged -= ObservableDictionaryQueryDictionaryChanged;
                observableDictionaryQuery.PropertyChanged -= ObservableDictionaryQueryPropertyChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        if (observableDictionaryQuery.OperationFault is { } fault)
            Evaluation = (fault, default);
        else
            Evaluation = (null, observableDictionaryQuery.Count);
    }

    void ObservableDictionaryQueryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        using var changeHold = HoldOwnChanges();
        Evaluate();
    }

    void ObservableDictionaryQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableDictionaryQuery<,>.OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            Evaluate();
        }
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(observableDictionaryQuery);
        observableDictionaryQuery.DictionaryChanged += ObservableDictionaryQueryDictionaryChanged;
        observableDictionaryQuery.PropertyChanged += ObservableDictionaryQueryPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"count of {observableDictionaryQuery}";
}
