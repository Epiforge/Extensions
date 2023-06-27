namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableUnaryExpression :
    ObservableExpression
{
    sealed record ImplementationsKey(ExpressionType NodeType, Type OperandType, Type ReturnValueType, MethodInfo? Method);

    static readonly ConcurrentDictionary<ImplementationsKey, UnaryOperationDelegate> implementations = new();

    static UnaryOperationDelegate ImplementationsValueFactory(ImplementationsKey key)
    {
        var operandParameter = Expression.Parameter(typeof(object));
        var operandConversion = Expression.Convert(operandParameter, key.OperandType);
        return Expression.Lambda<UnaryOperationDelegate>(Expression.Convert(key.Method is null ? Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType) : Expression.MakeUnary(key.NodeType, operandConversion, key.ReturnValueType, key.Method), typeof(object)), operandParameter).Compile();
    }

    public ObservableUnaryExpression(ExpressionObserver observer, UnaryExpression unaryExpression, bool deferEvaluation) :
        base(observer, unaryExpression, deferEvaluation) =>
        UnaryExpression = unaryExpression;

    UnaryOperationDelegate? @delegate;
    MethodInfo? method;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? operand;

    internal readonly UnaryExpression UnaryExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (removedFromCache)
            {
                if (operand is not null)
                {
                    DisposeValueIfNecessaryAndPossible();
                    operand.PropertyChanged -= OperandPropertyChanged;
                    operand.Dispose();
                }
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
                Evaluation = (operandFault, defaultResult);
            else
                Evaluation = (null, @delegate?.Invoke(operandResult));
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
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
