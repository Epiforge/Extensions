namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableUnaryExpression(ExpressionObserver observer, UnaryExpression unaryExpression, bool deferEvaluation) :
    ObservableExpression(observer, unaryExpression, deferEvaluation),
    IObservableExpressionDependent
{
    #region Delegates

    delegate object? UnaryOperationDelegate(object? operand);

    #endregion Delegates

    sealed record ImplementationsKey(ExpressionType NodeType, Type OperandType, Type ReturnValueType, MethodInfo? Method);

    static readonly ConcurrentDictionary<ImplementationsKey, UnaryOperationDelegate> implementations = new();

    static UnaryOperationDelegate ImplementationsValueFactory(ImplementationsKey key)
    {
        var operandParameter = Expression.Parameter(typeof(object));
        var operandConversion = Expression.Convert(operandParameter, key.OperandType);
        var operation = key.Method is null ? Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType) : Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType, key.Method);
        var lambda = Expression.Lambda<UnaryOperationDelegate>(BooleanBoxes.Convert(operation), operandParameter);
#if IS_NET_8_0_OR_GREATER
        if (!RuntimeFeature.IsDynamicCodeSupported && ClosureCompiler.TryCompile(lambda) is { } closures)
            return closures.Invoke;
#endif
        return lambda.Compile();
    }

    UnaryOperationDelegate? @delegate;
    MethodInfo? method;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? operand;
    ObservableExpressionSubscription? operandSubscription;

    internal readonly UnaryExpression UnaryExpression = unaryExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
            Retire();
        return removedFromCache;
    }

    private protected override void TearDown()
    {
        if (operand is not null)
        {
            if (operandSubscription is { } operandDependency)
                operand.UnsubscribeDependent(operandDependency);
            operand.Release();
            DisposeValueIfNecessaryAndPossible();
        }
        RemovedFromCache();
    }

    protected override void Evaluate()
    {
        try
        {
            var (operandFault, operandResult) = operand?.Evaluation ?? (null, null);
            if (operandFault is not null)
            {
                Evaluation = (operandFault, defaultResult);
                observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, operandFault, "{UnaryExpression} operand faulted: {Fault}", UnaryExpression, operandFault);
            }
            else
            {
                var value = @delegate?.Invoke(operandResult);
                Evaluation = (null, value);
                observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{UnaryExpression} evaluated: {Value}", UnaryExpression, value);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{UnaryExpression} faulted: {Fault}", UnaryExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        method is not null && observer.IsMethodReturnValueDisposed(method);

    protected override void OnInitialization()
    {
        try
        {
            operand = observer.GetObservableExpression(UnaryExpression.Operand, IsDeferringEvaluation);
            if (operand.CanChange)
                operandSubscription = operand.SubscribeDependent(this);
            method = UnaryExpression.Method;
            @delegate = implementations.GetOrAdd(new(UnaryExpression.NodeType, UnaryExpression.Operand.Type, UnaryExpression.Type, UnaryExpression.Method), ImplementationsValueFactory);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (operand is not null)
            {
                if (operandSubscription is { } operandDependency)
                    operand.UnsubscribeDependent(operandDependency);
                operand.Release();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        EvaluateOnce();
}
