namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableNewExpression(ExpressionObserver observer, NewExpression newExpression, bool deferEvaluation) :
    ObservableExpression(observer, newExpression, deferEvaluation),
    IObservableExpressionDependent
{
    ReadOnlyCollection<ObservableExpression>? arguments;
    ObservableExpressionSubscription?[]? argumentSubscriptions;
    ConstructorInfo? constructor;
    EquatableList<Type> constructorParameterTypes;

    internal readonly NewExpression NewExpression = newExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            DisposeValueIfNecessaryAndPossible();
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
            if (arguments?.Select(argument => argument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } argumentFault)
            {
                Evaluation = (argumentFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, argumentFault, "{NewExpression} argument faulted: {Fault}", NewExpression, argumentFault);
            }
            else
            {
                var value = constructor is not null ? constructor.FastInvoke(arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? []) : Activator.CreateInstance(NewExpression.Type, arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? []);
                Evaluation = (null, value);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{NewExpression} evaluated: {Value}", NewExpression, value);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{NewExpression} faulted: {Fault}", NewExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        observer.IsConstructedTypeDisposed(NewExpression.Type, constructorParameterTypes);

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            constructor = NewExpression.Constructor;
            var newExpressionArguments = NewExpression.Arguments;
            var subscriptions = new ObservableExpressionSubscription?[newExpressionArguments.Count];
            argumentSubscriptions = subscriptions;
            for (int i = 0, ii = newExpressionArguments.Count; i < ii; ++i)
            {
                var newExpressionArgument = newExpressionArguments[i];
                var argument = observer.GetObservableExpression(newExpressionArgument, IsDeferringEvaluation);
                if (argument.CanChange)
                    subscriptions[i] = argument.SubscribeDependent(this);
                argumentsList.Add(argument);
            }
            arguments = argumentsList.AsReadOnly();
            constructorParameterTypes = new(arguments.Select(arguments => arguments.Expression.Type));
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
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
}
