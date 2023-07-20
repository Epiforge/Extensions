namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableScalarQuery<TResult> :
    ObservableQuery,
    IObservableScalarQuery<TResult>
{
    protected ObservableScalarQuery(CollectionObserver collectionObserver) :
        base(collectionObserver)
    {
    }

    (Exception? Fault, TResult Result) evaluation;

    public (Exception? Fault, TResult Result) Evaluation
    {
        get => evaluation;
        protected set => SetBackedProperty(ref evaluation, in value);
    }
}
