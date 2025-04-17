namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableScalarQuery<TResult> :
    ObservableQuery,
    IObservableScalarQuery<TResult>
{
    protected ObservableScalarQuery(CollectionObserver collectionObserver) :
        base(collectionObserver)
    {
    }

    readonly Dictionary<Expression, ObservableQuery> cachedTransformQueries = new(ExpressionEqualityComparer.Default);
    readonly object cachedTransformQueriesAccess = new();
    (Exception? Fault, TResult Result) evaluation;

    public override int CachedObservableQueries
    {
        get
        {
            var count = 0;
            lock (cachedTransformQueriesAccess)
                count += cachedTransformQueries.Values.Sum(transformQuery => 1 + transformQuery.CachedObservableQueries);
            return count;
        }
    }

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

    public IObservableScalarQuery<TTransform> ObserveTransform<TTransform>(Expression<Func<TResult, TTransform>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ObservableQuery transformQuery;
        lock (cachedTransformQueriesAccess)
        {
            if (!cachedTransformQueries.TryGetValue(transform, out transformQuery!))
            {
                transformQuery = new ObservableScalarTransformQuery<TResult, TTransform>(collectionObserver, this, transform);
                cachedTransformQueries.Add(transform, transformQuery);
            }
            ++transformQuery.Observations;
        }
        transformQuery.Initialize();
        return (IObservableScalarQuery<TTransform>)transformQuery;
    }

    internal bool QueryDisposed<TTransform>(ObservableScalarTransformQuery<TResult, TTransform> query)
    {
        lock (cachedTransformQueriesAccess)
        {
            if (--query.Observations == 0)
            {
                cachedTransformQueries.Remove(query.Transform);
                return true;
            }
        }
        return false;
    }
}
