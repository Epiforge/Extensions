namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableTypeBinaryExpression(ExpressionObserver observer, TypeBinaryExpression typeBinaryExpression, bool deferEvaluation) :
    ObservableExpression(observer, typeBinaryExpression, deferEvaluation),
    IObservableExpressionDependent
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

    TypeIsDelegate? @delegate;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? expression;
    ObservableExpressionSubscription? expressionSubscription;
    Type? typeOperand;

    internal readonly TypeBinaryExpression TypeBinaryExpression = typeBinaryExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            if (expression is not null)
            {
                if (expressionSubscription is { } expressionDependency)
                    expression.UnsubscribeDependent(expressionDependency);
                expression.Dispose();
            }
            RemovedFromCache();
        }
        return removedFromCache;
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
            Evaluation = (null, value);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{TypeBinaryExpression} evaluated: {Value}", TypeBinaryExpression, value);
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();

    protected override void OnInitialization()
    {
        try
        {
            expression = observer.GetObservableExpression(TypeBinaryExpression.Expression, IsDeferringEvaluation);
            if (expression.CanChange)
                expressionSubscription = expression.SubscribeDependent(this);
            typeOperand = TypeBinaryExpression.TypeOperand;
            @delegate = delegates.GetOrAdd(typeOperand, CreateDelegate);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (expression is not null)
            {
                if (expressionSubscription is { } expressionDependency)
                    expression.UnsubscribeDependent(expressionDependency);
                expression.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
