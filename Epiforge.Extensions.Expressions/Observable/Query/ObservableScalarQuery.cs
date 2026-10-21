namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableScalarQuery<TResult>(CollectionObserver collectionObserver) :
    ObservableQuery(collectionObserver),
    IObservableScalarQuery<TResult>
{
    Dictionary<Expression, ObservableQuery>? cachedTransformQueries;
    (Exception? Fault, TResult Result) evaluation;
    Exception? ownFault;

    public override int CachedObservableQueries
    {
        get
        {
            if (ExistingChildrenAccess is not { } access)
                return 0;
            lock (access)
                return cachedTransformQueries?.Values.Sum(transformQuery => 1 + transformQuery.CachedObservableQueries) ?? 0;
        }
    }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get => evaluation;
        protected set
        {
            if (value.Fault is AggregateException && FaultList.DescribeTheSame(evaluation.Fault, value.Fault))
                value = (evaluation.Fault, value.Result);
            if (SetBackedProperty(ref evaluation, in value, ObservableExpression.EvaluationPropertyChangingEventArgs, ObservableExpression.EvaluationPropertyChangedEventArgs) && Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
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
        lock (ChildrenAccess)
        {
            if ((cachedTransformQueries ??= new(ExpressionEqualityComparer.Default)).TryGetValue(transform, out transformQuery!))
            {
                ++transformQuery.Observations;
                return ((ObservableScalarQuery<TTransform>)transformQuery).AsScoped();
            }
        }
        var built = new ObservableScalarTransformQuery<TResult, TTransform>(collectionObserver, this, transform);
        built.Observations = 1;
        built.Initialize();
        lock (ChildrenAccess)
        {
            ref var cached = ref CollectionsMarshal.GetValueRefOrAddDefault(cachedTransformQueries ??= new(ExpressionEqualityComparer.Default), transform, out var exists);
            if (!exists)
            {
                cached = built;
                built.IsCached = true;
                return ((ObservableScalarQuery<TTransform>)built).AsScoped();
            }
            transformQuery = cached!;
            ++transformQuery.Observations;
        }
        built.Release();
        return ((ObservableScalarQuery<TTransform>)transformQuery).AsScoped();
    }

    /// <summary>
    /// Gets a fault this query raises for a reason of its own, which is the one it already reports when that was raised for the same reason, so that a fault standing through a change is neither replaced nor announced
    /// </summary>
    private protected Exception OwnFault(Exception fault)
    {
        if (ownFault is { } standing && standing.GetType() == fault.GetType() && standing.Message == fault.Message && (ReferenceEquals(evaluation.Fault, standing) || evaluation.Fault is AggregateException aggregate && aggregate.InnerExceptions.Contains(standing)))
            return standing;
        return ownFault = fault;
    }

    internal bool QueryDisposed<TTransform>(ObservableScalarTransformQuery<TResult, TTransform> query)
    {
        lock (ChildrenAccess)
        {
            var remaining = --query.Observations;
            if (remaining < 0)
                throw new InvalidOperationException("an observation was released more times than it was acquired");
            if (remaining == 0)
            {
                if (query.IsCached)
                    cachedTransformQueries!.Remove(query.Transform);
                return true;
            }
        }
        return false;
    }
}
