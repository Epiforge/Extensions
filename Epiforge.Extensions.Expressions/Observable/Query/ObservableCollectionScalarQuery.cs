namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableCollectionScalarQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery) :
    ObservableScalarQuery<TResult>(collectionObserver)
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableCollectionQuery<TElement> observableCollectionQuery = observableCollectionQuery;
}
