namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableScalarQuery<TResult>(CollectionObserver collectionObserver) :
    ObservableQuery(collectionObserver),
    IObservableScalarQuery<TResult>
{
    Dictionary<Expression, ObservableQuery>? cachedTransformQueries;
#if IS_NET_9_0_OR_GREATER
    Lock? cachedQueriesAccess;
#else
    object? cachedQueriesAccess;
#endif
    (Exception? Fault, TResult Result) evaluation;

    public override int CachedObservableQueries
    {
        get
        {
            if (Volatile.Read(ref cachedQueriesAccess) is not { } access)
                return 0;
            lock (access)
                return cachedTransformQueries?.Values.Sum(transformQuery => 1 + transformQuery.CachedObservableQueries) ?? 0;
        }
    }

    /// <summary>
    /// Gets the lock guarding this query's caches of the queries built over it, created by whichever observation first needs it, so that a query nothing is built over allocates none
    /// </summary>
#if IS_NET_9_0_OR_GREATER
    Lock CachedQueriesAccess
#else
    object CachedQueriesAccess
#endif
    {
        get
        {
            if (Volatile.Read(ref cachedQueriesAccess) is { } access)
                return access;
            access = new();
            return Interlocked.CompareExchange(ref cachedQueriesAccess, access, null) ?? access;
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

    internal IObservableScalarQuery<TResult> AsScoped() =>
        new ScopedObservableScalarQuery<TResult>(this);

    public IObservableScalarQuery<TTransform> ObserveTransform<TTransform>(Expression<Func<TResult, TTransform>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ObservableQuery transformQuery;
        lock (CachedQueriesAccess)
        {
            if (!(cachedTransformQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(transform, out transformQuery!))
            {
                transformQuery = new ObservableScalarTransformQuery<TResult, TTransform>(collectionObserver, this, transform);
                cachedTransformQueries.Add(transform, transformQuery);
            }
            ++transformQuery.Observations;
        }
        transformQuery.Initialize();
        return ((ObservableScalarQuery<TTransform>)transformQuery).AsScoped();
    }

    internal bool QueryDisposed<TTransform>(ObservableScalarTransformQuery<TResult, TTransform> query)
    {
        lock (CachedQueriesAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                cachedTransformQueries!.Remove(query.Transform);
                return true;
            }
        }
        return false;
    }
}
