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
                    logger.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryFaulted, fault, "{ScalarQuery} faulted: {Fault}", this, fault);
                else
                    logger.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryEvaluated, "{ScalarQuery} evaluated: {Result}", this, value.Result);
            }
        }
    }
}
