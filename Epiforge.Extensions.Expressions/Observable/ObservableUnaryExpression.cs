namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableUnaryExpression(ExpressionObserver observer, UnaryExpression unaryExpression, bool deferEvaluation) :
    ObservableExpression(observer, unaryExpression, deferEvaluation)
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
        return Expression.Lambda<UnaryOperationDelegate>(Expression.Convert(key.Method is null ? Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType) : Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType, key.Method), typeof(object)), operandParameter).Compile();
    }

    UnaryOperationDelegate? @delegate;
    MethodInfo? method;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? operand;

    internal readonly UnaryExpression UnaryExpression = unaryExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                if (operand is not null)
                {
                    DisposeValueIfNecessaryAndPossible();
                    operand.PropertyChanged -= OperandPropertyChanged;
                    operand.Dispose();
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        try
        {
            var (operandFault, operandResult) = operand?.Evaluation ?? (null, null);
            if (operandFault is not null)
            {
                Evaluation = (operandFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, operandFault, "{UnaryExpression} operand faulted: {Fault}", UnaryExpression, operandFault);
            }
            else
            {
                var value = @delegate?.Invoke(operandResult);
                Evaluation = (null, value);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{UnaryExpression} evaluated: {Value}", UnaryExpression, value);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{UnaryExpression} faulted: {Fault}", UnaryExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        method is not null && observer.IsMethodReturnValueDisposed(method);

    protected override void OnInitialization()
    {
        try
        {
            operand = observer.GetObservableExpression(UnaryExpression.Operand, IsDeferringEvaluation);
            operand.PropertyChanged += OperandPropertyChanged;
            method = UnaryExpression.Method;
            @delegate = implementations.GetOrAdd(new(UnaryExpression.NodeType, UnaryExpression.Operand.Type, UnaryExpression.Type, UnaryExpression.Method), ImplementationsValueFactory);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (operand is not null)
            {
                operand.PropertyChanged -= OperandPropertyChanged;
                operand.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void OperandPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();
}
