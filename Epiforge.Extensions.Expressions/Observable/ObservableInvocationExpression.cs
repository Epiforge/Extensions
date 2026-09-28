namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableInvocationExpression(ExpressionObserver observer, InvocationExpression invocationExpression, bool deferEvaluation) :
    ObservableExpression(observer, invocationExpression, deferEvaluation),
    IObservableExpressionDependent
{
    const int argumentsChanged = 1;
    const int delegateChanged = 2;

    ObservableExpressionSubscription?[]? observableArgumentSubscriptions;
    IReadOnlyList<ObservableExpression>? observableArguments;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableDelegateExpression;
    ObservableExpressionSubscription? observableDelegateExpressionSubscription;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableExpression;
    ObservableExpressionSubscription? observableExpressionSubscription;
    /// <summary>
    /// What has changed since the body was built, which the next evaluation rebuilds it for, so that a body is only ever rebuilt by the thread evaluating
    /// </summary>
    int rebuilding;

    internal readonly InvocationExpression InvocationExpression = invocationExpression;

    void CreateObservableExpression()
    {
        switch (InvocationExpression.Expression)
        {
            case LambdaExpression lambdaExpression when observableArguments is not null:
                observableExpression = observer.GetObservableExpression(ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, EvaluationResults(observableArguments))!, IsDeferringEvaluation);
                break;
            case Expression expression when typeof(Delegate).IsAssignableFrom(expression.Type):
                if (observableDelegateExpression is null)
                {
                    observableDelegateExpression = observer.GetObservableExpression(expression, IsDeferringEvaluation);
                    if (observableDelegateExpression.CanChange)
                        observableDelegateExpressionSubscription = observableDelegateExpression.SubscribeDependent(this);
                }
                if (observableDelegateExpression.Evaluation.Result is Delegate @delegate)
                    observableExpression = observer.GetObservableExpression(@delegate.Target is { } target ? Expression.Call(Expression.Constant(target), @delegate.Method, InvocationExpression.Arguments) : Expression.Call(@delegate.Method, InvocationExpression.Arguments), IsDeferringEvaluation);
                break;
            default:
                throw new NotSupportedException($"invocation expression expression type {InvocationExpression.Expression.GetType().Name} is not supported");
        }
        if (observableExpression is not null && observableExpression.CanChange)
            observableExpressionSubscription = observableExpression.SubscribeDependent(this);
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
        if (observableExpression is not null)
        {
            if (observableExpressionSubscription is { } observableExpressionDependency)
                observableExpression.UnsubscribeDependent(observableExpressionDependency);
            observableExpression.Release();
        }
        if (observableDelegateExpression is not null)
        {
            if (observableDelegateExpressionSubscription is { } observableDelegateExpressionDependency)
                observableDelegateExpression.UnsubscribeDependent(observableDelegateExpressionDependency);
            observableDelegateExpression.Release();
        }
        if (observableArguments is not null)
            for (int i = 0, ii = observableArguments.Count; i < ii; i++)
            {
                var obserableArgument = observableArguments[i];
                if (observableArgumentSubscriptions?[i] is { } obserableArgumentDependency)
                    obserableArgument.UnsubscribeDependent(obserableArgumentDependency);
                obserableArgument.Release();
            }
        RemovedFromCache();
    }

    protected override void Evaluate()
    {
        if (Volatile.Read(ref rebuilding) != 0 && Interlocked.Exchange(ref rebuilding, 0) is var rebuild and not 0 && !Rebuild(rebuild))
            return;
        var (observableExpressionFault, observableExpressionResult) = observableExpression?.Evaluation ?? (null, null);
        if (observableExpressionFault is not null)
        {
            Evaluation = (observableExpressionFault, defaultResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, observableExpressionFault, "{InvocationExpression} is faulted: {Fault}", InvocationExpression, observableExpressionFault);
        }
        else if (observableArguments is { } faultedArguments && FirstFault(faultedArguments) is { } observableArgumentFault)
        {
            Evaluation = (observableArgumentFault, defaultResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, observableArgumentFault, "{InvocationExpression} argument is faulted: {Fault}", InvocationExpression, observableArgumentFault);
        }
        else
        {
            Evaluation = (null, observableExpressionResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{InvocationExpression} evaluated: {Value}", InvocationExpression, observableExpressionResult);
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency)
    {
        if (ReferenceEquals(dependency, observableExpression))
            EvaluateOnce();
        else if (!IsEvaluatingOnThisThread)
        {
            Interlocked.Or(ref rebuilding, ReferenceEquals(dependency, observableDelegateExpression) ? delegateChanged : argumentsChanged);
            EvaluateOnce();
        }
    }

    /// <summary>
    /// Rebuilds the body for what changed, yielding whether the evaluation which found the change should go on to evaluate, which it should not while evaluation is deferred
    /// </summary>
    bool Rebuild(int rebuild)
    {
        if (observableExpression is not null)
        {
            if (observableExpressionSubscription is { } observableExpressionDependency)
                observableExpression.UnsubscribeDependent(observableExpressionDependency);
            observableExpressionSubscription = null;
            observableExpression.Release();
            observableExpression = null;
        }
        if ((rebuild & delegateChanged) != 0 || observableArguments is not { } faultCheckedArguments || FirstFault(faultCheckedArguments) is null)
            CreateObservableExpression();
        return !IsDeferringEvaluation;
    }

    protected override void OnInitialization()
    {
        var observableArgumentsList = new List<ObservableExpression>();
        try
        {
            if (InvocationExpression.Expression is LambdaExpression)
            {
                var invocationExpressionArguments = InvocationExpression.Arguments;
                ObservableExpressionSubscription?[] subscriptions = invocationExpressionArguments.Count == 0 ? [] : new ObservableExpressionSubscription?[invocationExpressionArguments.Count];
                observableArgumentSubscriptions = subscriptions;
                for (int i = 0, ii = invocationExpressionArguments.Count; i < ii; ++i)
                {
                    var invocationExpressionArgument = invocationExpressionArguments[i];
                    observableArgumentsList.Add(observer.GetObservableExpression(invocationExpressionArgument, IsDeferringEvaluation));
                }
                observableArguments = [..observableArgumentsList];
                for (int i = 0, ii = observableArgumentsList.Count; i < ii; ++i)
                    if (observableArgumentsList[i] is { CanChange: true } observableArgument)
                        subscriptions[i] = observableArgument.SubscribeDependent(this);
            }
            CreateObservableExpression();
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (observableExpression is not null)
            {
                if (observableExpressionSubscription is { } observableExpressionDependency)
                    observableExpression.UnsubscribeDependent(observableExpressionDependency);
                observableExpression.Release();
            }
            if (observableDelegateExpression is not null)
            {
                if (observableDelegateExpressionSubscription is { } observableDelegateExpressionDependency)
                    observableDelegateExpression.UnsubscribeDependent(observableDelegateExpressionDependency);
                observableDelegateExpression.Release();
            }
            for (int i = 0, ii = observableArgumentsList.Count; i < ii; ++i)
            {
                var observableArgument = observableArgumentsList[i];
                if (observableArgumentSubscriptions?[i] is { } observableArgumentDependency)
                    observableArgument.UnsubscribeDependent(observableArgumentDependency);
                observableArgument.Release();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
