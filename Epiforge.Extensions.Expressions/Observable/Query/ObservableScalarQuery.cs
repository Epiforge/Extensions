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
        protected set
        {
            if (SetBackedProperty(ref evaluation, in value) && Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
            {
                if (value.Fault is { } fault)
                    logger.LogTrace("{ScalarQuery} faulted: {Fault}", this, fault);
                else
                    logger.LogTrace("{ScalarQuery} evaluated: {Result}", this, value.Result);
            }
        }
    }
}
