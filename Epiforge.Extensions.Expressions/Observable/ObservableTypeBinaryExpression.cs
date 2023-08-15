namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableTypeBinaryExpression :
    ObservableExpression
{
    #region Delegates

    delegate bool TypeIsDelegate(object? obj);

    #endregion Delegates

    static readonly ConcurrentDictionary<Type, TypeIsDelegate> delegates = new();

    static TypeIsDelegate CreateDelegate(Type type)
    {
        var parameter = Expression.Parameter(typeof(object));
        return Expression.Lambda<TypeIsDelegate>(Expression.TypeIs(parameter, type), parameter).Compile();
    }

    public ObservableTypeBinaryExpression(ExpressionObserver observer, TypeBinaryExpression typeBinaryExpression, bool deferEvaluation) :
        base(observer, typeBinaryExpression, deferEvaluation) =>
        TypeBinaryExpression = typeBinaryExpression;

    TypeIsDelegate? @delegate;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? expression;
    Type? typeOperand;

    internal readonly TypeBinaryExpression TypeBinaryExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                if (expression is not null)
                {
                    expression.PropertyChanged -= ExpressionPropertyChanged;
                    expression.Dispose();
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        var (expressionFault, expressionValue) = expression?.Evaluation ?? (null, null);
        if (expressionFault is not null)
        {
            Evaluation = (expressionFault, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, expressionFault, "{TypeBinaryExpression} expression faulted: {Fault}", TypeBinaryExpression, expressionFault);
        }
        else
        {
            var value = @delegate?.Invoke(expressionValue);
            Evaluation = (null, @delegate?.Invoke(expressionValue));
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{TypeBinaryExpression} evaluated: {Value}", TypeBinaryExpression, value);
        }
    }

    void ExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        try
        {
            expression = observer.GetObservableExpression(TypeBinaryExpression.Expression, IsDeferringEvaluation);
            expression.PropertyChanged += ExpressionPropertyChanged;
            typeOperand = TypeBinaryExpression.TypeOperand;
            @delegate = delegates.GetOrAdd(typeOperand, CreateDelegate);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (expression is not null)
            {
                expression.PropertyChanged -= ExpressionPropertyChanged;
                expression.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
