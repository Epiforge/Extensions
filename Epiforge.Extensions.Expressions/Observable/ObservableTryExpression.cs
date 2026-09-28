namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableTryExpression(ExpressionObserver observer, TryExpression tryExpression, bool deferEvaluation) :
    ObservableExpression(observer, tryExpression, deferEvaluation),
    IObservableExpressionDependent
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? body;
    ObservableExpressionSubscription? bodySubscription;
    ObservableExpression?[]? handlers;
    ObservableExpressionSubscription?[]? handlerSubscriptions;

    internal readonly TryExpression TryExpression = tryExpression;

    void DisposeOperands()
    {
        if (body is not null)
        {
            if (bodySubscription is { } bodyDependency)
                body.UnsubscribeDependent(bodyDependency);
            body.Release();
        }
        if (handlers is not null)
            for (int i = 0, ii = handlers.Length; i < ii; ++i)
                if (handlers[i] is { } handler)
                {
                    if (handlerSubscriptions?[i] is { } handlerDependency)
                        handler.UnsubscribeDependent(handlerDependency);
                    handler.Release();
                }
    }

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
            Retire();
        return removedFromCache;
    }

    private protected override void TearDown()
    {
        DisposeOperands();
        RemovedFromCache();
    }

    protected override void Evaluate()
    {
        var (bodyFault, bodyResult) = body?.Evaluation ?? (null, null);
        if (bodyFault is null)
        {
            Evaluation = (null, bodyResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{TryExpression} evaluated: {Value}", TryExpression, bodyResult);
            return;
        }
        var catchBlocks = TryExpression.Handlers;
        var faultType = bodyFault.GetType();
        for (int i = 0, ii = catchBlocks.Count; i < ii; ++i)
            if (catchBlocks[i].Test.IsAssignableFrom(faultType))
            {
                if (handlers![i] is { } handler)
                {
                    Evaluation = handler.Evaluation;
                    observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{TryExpression} body faulted and was handled: {Fault}", TryExpression, bodyFault);
                    return;
                }
                break;
            }
        Evaluation = (bodyFault, defaultResult);
        observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, bodyFault, "{TryExpression} body faulted: {Fault}", TryExpression, bodyFault);
    }

    protected override void OnInitialization()
    {
        try
        {
            var tryExpression = TryExpression;
            body = observer.GetObservableExpression(tryExpression.Body, IsDeferringEvaluation);
            var catchBlocks = tryExpression.Handlers;
            handlers = new ObservableExpression?[catchBlocks.Count];
            handlerSubscriptions = new ObservableExpressionSubscription?[catchBlocks.Count];
            for (int i = 0, ii = catchBlocks.Count; i < ii; ++i)
                if (!ExpressionObserver.IsRethrow(catchBlocks[i].Body))
                    handlers[i] = observer.GetObservableExpression(catchBlocks[i].Body, true);
            if (body.CanChange)
                bodySubscription = body.SubscribeDependent(this);
            for (int i = 0, ii = handlers.Length; i < ii; ++i)
                if (handlers[i] is { CanChange: true } handler)
                    handlerSubscriptions[i] = handler.SubscribeDependent(this);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeOperands();
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        EvaluateOnce();
}
