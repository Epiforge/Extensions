namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableDictionaryScalarQuery<TKey, TValue, TResult>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery) :
    ObservableScalarQuery<TResult>(collectionObserver)
    where TKey : notnull
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableDictionaryQuery<TKey, TValue> observableDictionaryQuery = observableDictionaryQuery;
}