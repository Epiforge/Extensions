namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableDictionaryScalarQuery<TKey, TValue, TResult> :
    ObservableScalarQuery<TResult>
    where TKey : notnull
{
    protected ObservableDictionaryScalarQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery) :
        base(collectionObserver) =>
        this.observableDictionaryQuery = observableDictionaryQuery;

    protected ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery;
}