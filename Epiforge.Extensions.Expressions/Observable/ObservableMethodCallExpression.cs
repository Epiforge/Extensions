namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMethodCallExpression(ExpressionObserver observer, MethodCallExpression methodCallExpression, bool deferEvaluation) :
    ObservableExpression(observer, methodCallExpression, deferEvaluation),
    IObservableExpressionDependent
{
    ReadOnlyCollection<ObservableExpression>? arguments;
    ObservableExpressionSubscription?[]? argumentSubscriptions;
    MethodInfo? method;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? @object;
    ObservableExpressionSubscription? objectSubscription;

    internal readonly MethodCallExpression MethodCallExpression = methodCallExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            DisposeValueIfNecessaryAndPossible();
            if (@object is not null)
            {
                if (objectSubscription is { } objectDependency)
                    @object.UnsubscribeDependent(objectDependency);
                @object.Dispose();
            }
            if (arguments is not null)
                for (int i = 0, ii = arguments.Count; i < ii; ++i)
                {
                    var argument = arguments[i];
                    if (argumentSubscriptions?[i] is { } argumentDependency)
                        argument.UnsubscribeDependent(argumentDependency);
                    argument.Dispose();
                }
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void Evaluate()
    {
        try
        {
            var (objectFault, objectResult) = @object?.Evaluation ?? (null, null);
            if (objectFault is not null)
            {
                Evaluation = (objectFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, objectFault, "{MethodCallExpression} object faulted: {Fault}", MethodCallExpression, objectFault);
            }
            else if (arguments?.Select(argument => argument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } argumentFault)
            {
                Evaluation = (argumentFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, argumentFault, "{MethodCallExpression} argument faulted: {Fault}", MethodCallExpression, argumentFault);
            }
            else
            {
                var value = method?.FastInvoke(objectResult, arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? []);
                Evaluation = (null, value);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{MethodCallExpression} evaluated: {Value}", MethodCallExpression, value);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{MethodCallExpression} faulted: {Fault}", MethodCallExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        method is not null && observer.IsMethodReturnValueDisposed(method);

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            method = MethodCallExpression.Method;
            if (MethodCallExpression.Object is { } methodCallExpressionObject)
            {
                @object = observer.GetObservableExpression(methodCallExpressionObject, IsDeferringEvaluation);
                if (@object.CanChange)
                    objectSubscription = @object.SubscribeDependent(this);
            }
            var methodCallExpressionArguments = MethodCallExpression.Arguments;
            var subscriptions = new ObservableExpressionSubscription?[methodCallExpressionArguments.Count];
            argumentSubscriptions = subscriptions;
            for (int i = 0, ii = methodCallExpressionArguments.Count; i < ii; ++i)
            {
                var methodCallExpressionArgument = methodCallExpressionArguments[i];
                var argument = observer.GetObservableExpression(methodCallExpressionArgument, IsDeferringEvaluation);
                if (argument.CanChange)
                    subscriptions[i] = argument.SubscribeDependent(this);
                argumentsList.Add(argument);
            }
            arguments = argumentsList.AsReadOnly();
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (@object is not null)
            {
                if (objectSubscription is { } objectDependency)
                    @object.UnsubscribeDependent(objectDependency);
                @object.Dispose();
            }
            for (int i = 0, ii = argumentsList.Count; i < ii; ++i)
            {
                var argument = argumentsList[i];
                if (argumentSubscriptions?[i] is { } argumentDependency)
                    argument.UnsubscribeDependent(argumentDependency);
                argument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();
}
