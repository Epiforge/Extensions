namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableCollectionScalarQuery<TElement, TResult> :
    ObservableScalarQuery<TResult>
{
    protected ObservableCollectionScalarQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery) :
        base(collectionObserver) =>
        this.observableCollectionQuery = observableCollectionQuery;

    protected ObservableCollectionQuery<TElement> observableCollectionQuery;
}
