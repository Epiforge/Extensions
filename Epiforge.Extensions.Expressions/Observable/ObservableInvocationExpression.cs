namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableInvocationExpression(ExpressionObserver observer, InvocationExpression invocationExpression, bool deferEvaluation) :
    ObservableExpression(observer, invocationExpression, deferEvaluation),
    IObservableExpressionDependent
{
    ObservableExpressionSubscription?[]? observableArgumentSubscriptions;
    IReadOnlyList<ObservableExpression>? observableArguments;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableDelegateExpression;
    ObservableExpressionSubscription? observableDelegateExpressionSubscription;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableExpression;
    ObservableExpressionSubscription? observableExpressionSubscription;

    internal readonly InvocationExpression InvocationExpression = invocationExpression;

    void CreateObservableExpression()
    {
        switch (InvocationExpression.Expression)
        {
            case LambdaExpression lambdaExpression when observableArguments is not null:
                observableExpression = observer.GetObservableExpression(ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, observableArguments.Select(observableArgument => observableArgument.Evaluation.Result).ToArray() ?? [])!, IsDeferringEvaluation);
                break;
            case Expression expression when typeof(Delegate).IsAssignableFrom(expression.Type):
                var observableDelegateExpressionCreated = false;
                if (observableDelegateExpression is null)
                {
                    observableDelegateExpression = observer.GetObservableExpression(expression, IsDeferringEvaluation);
                    observableDelegateExpressionCreated = true;
                }
                if (observableDelegateExpression.Evaluation.Result is Delegate @delegate)
                    observableExpression = observer.GetObservableExpression(@delegate.Target is { } target ? Expression.Call(Expression.Constant(target), @delegate.Method, InvocationExpression.Arguments) : Expression.Call(@delegate.Method, InvocationExpression.Arguments), IsDeferringEvaluation);
                if (observableDelegateExpressionCreated && observableDelegateExpression.CanChange)
                    observableDelegateExpressionSubscription = observableDelegateExpression.SubscribeDependent(this);
                break;
            default:
                throw new NotSupportedException($"invocation expression expression type {InvocationExpression.Expression.GetType().Name} is not supported");
        }
        if (observableExpression is not null && observableExpression.CanChange)
            observableExpressionSubscription = observableExpression.SubscribeDependent(this);
        EvaluateIfNotDeferred();
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                if (observableExpression is not null)
                {
                    if (observableExpressionSubscription is { } observableExpressionDependency)
                        observableExpression.UnsubscribeDependent(observableExpressionDependency);
                    observableExpression.Dispose();
                }
                if (observableDelegateExpression is not null)
                {
                    if (observableDelegateExpressionSubscription is { } observableDelegateExpressionDependency)
                        observableDelegateExpression.UnsubscribeDependent(observableDelegateExpressionDependency);
                    observableDelegateExpression.Dispose();
                }
                if (observableArguments is not null)
                    for (int i = 0, ii = observableArguments.Count; i < ii; i++)
                    {
                        var obserableArgument = observableArguments[i];
                        if (observableArgumentSubscriptions?[i] is { } obserableArgumentDependency)
                            obserableArgument.UnsubscribeDependent(obserableArgumentDependency);
                        obserableArgument.Dispose();
                    }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        var (observableExpressionFault, observableExpressionResult) = observableExpression?.Evaluation ?? (null, null);
        if (observableExpressionFault is not null)
        {
            Evaluation = (observableExpressionFault, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, observableExpressionFault, "{InvocationExpression} is faulted: {Fault}", InvocationExpression, observableExpressionFault);
        }
        else if (observableArguments?.Select(observableArgument => observableArgument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } observableArgumentFault)
        {
            Evaluation = (observableArgumentFault, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, observableArgumentFault, "{InvocationExpression} argument is faulted: {Fault}", InvocationExpression, observableArgumentFault);
        }
        else
        {
            Evaluation = (null, observableExpressionResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{InvocationExpression} evaluated: {Value}", InvocationExpression, observableExpressionResult);
        }
    }

    void OnObservableArgumentEvaluationChanged()
    {
        if (observableExpression is not null)
        {
            if (observableExpressionSubscription is { } observableExpressionDependency)
                observableExpression.UnsubscribeDependent(observableExpressionDependency);
            observableExpressionSubscription = null;
            observableExpression.Dispose();
            observableExpression = null;
        }
        if (observableArguments?.All(observableArgument => observableArgument.Evaluation.Fault is null) ?? true)
            CreateObservableExpression();
        else if (!IsDeferringEvaluation)
            Evaluate();
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency)
    {
        if (ReferenceEquals(dependency, observableExpression))
            Evaluate();
        else if (ReferenceEquals(dependency, observableDelegateExpression))
            OnObservableDelegateExpressionEvaluationChanged();
        else
            OnObservableArgumentEvaluationChanged();
    }

    void OnObservableDelegateExpressionEvaluationChanged()
    {
        if (observableExpression is not null)
        {
            if (observableExpressionSubscription is { } observableExpressionDependency)
                observableExpression.UnsubscribeDependent(observableExpressionDependency);
            observableExpressionSubscription = null;
            observableExpression.Dispose();
            observableExpression = null;
        }
        CreateObservableExpression();
    }

    protected override void OnInitialization()
    {
        var observableArgumentsList = new List<ObservableExpression>();
        try
        {
            if (InvocationExpression.Expression is LambdaExpression)
            {
                var invocationExpressionArguments = InvocationExpression.Arguments;
                var subscriptions = new ObservableExpressionSubscription?[invocationExpressionArguments.Count];
                observableArgumentSubscriptions = subscriptions;
                for (int i = 0, ii = invocationExpressionArguments.Count; i < ii; ++i)
                {
                    var invocationExpressionArgument = invocationExpressionArguments[i];
                    var observableArgument = observer.GetObservableExpression(invocationExpressionArgument, IsDeferringEvaluation);
                    if (observableArgument.CanChange)
                        subscriptions[i] = observableArgument.SubscribeDependent(this);
                    observableArgumentsList.Add(observableArgument);
                }
                observableArguments = [..observableArgumentsList];
            }
            CreateObservableExpression();
        }
        catch (Exception ex)
        {
            if (observableExpression is not null)
            {
                if (observableExpressionSubscription is { } observableExpressionDependency)
                    observableExpression.UnsubscribeDependent(observableExpressionDependency);
                observableExpression.Dispose();
            }
            if (observableDelegateExpression is not null)
            {
                if (observableDelegateExpressionSubscription is { } observableDelegateExpressionDependency)
                    observableDelegateExpression.UnsubscribeDependent(observableDelegateExpressionDependency);
                observableDelegateExpression.Dispose();
            }
            for (int i = 0, ii = observableArgumentsList.Count; i < ii; ++i)
            {
                var observableArgument = observableArgumentsList[i];
                if (observableArgumentSubscriptions?[i] is { } observableArgumentDependency)
                    observableArgument.UnsubscribeDependent(observableArgumentDependency);
                observableArgument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
