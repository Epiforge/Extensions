namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableNewExpression(ExpressionObserver observer, NewExpression newExpression, bool deferEvaluation) :
    ObservableExpression(observer, newExpression, deferEvaluation),
    IObservableExpressionDependent
{
    ReadOnlyCollection<ObservableExpression>? arguments;
    ObservableExpressionSubscription?[]? argumentSubscriptions;
    ConstructorInfo? constructor;
    FastInvoker? constructorInvoker;
    EquatableList<Type> constructorParameterTypes;

    internal readonly NewExpression NewExpression = newExpression;

    internal bool ShouldConstructedValueBeDisposed =>
        observer.IsConstructedTypeDisposed(NewExpression.Type, constructorParameterTypes);

    /// <summary>
    /// Produces a new instance without disturbing this observation's own evaluation
    /// </summary>
    /// <remarks>
    /// A member initialization observation constructs through this on every evaluation rather than reusing what this node last produced. Reusing it would alias one instance across every evaluation and across every other observation sharing this cached node, and a result whose reference never changes is a result the evaluation setter can never see as changed
    /// </remarks>
    internal object? Construct() =>
        constructorInvoker is { } invoker ? Invoke(invoker, null, arguments) : Activator.CreateInstance(NewExpression.Type, arguments is { } evaluatedArguments ? EvaluationResults(evaluatedArguments) : []);

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
            if (arguments is { } faultedArguments && FirstFault(faultedArguments) is { } argumentFault)
            {
                Evaluation = (argumentFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, argumentFault, "{NewExpression} argument faulted: {Fault}", NewExpression, argumentFault);
            }
            else
            {
                var value = Construct();
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
        ShouldConstructedValueBeDisposed;

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        EvaluateOnce();

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            constructor = NewExpression.Constructor;
            constructorInvoker = constructor?.GetFastInvoker();
            var newExpressionArguments = NewExpression.Arguments;
            ObservableExpressionSubscription?[] subscriptions = newExpressionArguments.Count == 0 ? [] : new ObservableExpressionSubscription?[newExpressionArguments.Count];
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
