namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableCoalesceExpression :
    ObservableBinaryExpression
{
    #region Cache Comparers

    class ConverterEqualityComparer :
    IEqualityComparer<LambdaExpression>
    {
        public static ConverterEqualityComparer Default { get; } = new();

        public bool Equals(LambdaExpression? x, LambdaExpression? y) =>
            x?.Parameters[0].Type == y?.Parameters[0].Type && x?.Body.Type == y?.Body.Type;

        public int GetHashCode(LambdaExpression obj) =>
            Components.HashCode.Combine(obj.Parameters[0].Type, obj.Body.Type);
    }

    #endregion Cache Comparers

    #region Delegates

    delegate object? UnaryOperationDelegate(object? operand);

    #endregion Delegates

    static readonly ConcurrentDictionary<LambdaExpression, UnaryOperationDelegate> conversionDelegates = new(ConverterEqualityComparer.Default);

    static UnaryOperationDelegate ConversionDelegatesFactory(LambdaExpression lambdaExpression)
    {
        var parameter = Expression.Parameter(typeof(object));
        return Expression.Lambda<UnaryOperationDelegate>(Expression.Convert(Expression.Invoke(lambdaExpression, Expression.Convert(parameter, lambdaExpression.Parameters[0].Type)), typeof(object)), parameter).Compile();
    }

    public ObservableCoalesceExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
        base(observer, binaryExpression, deferEvaluation)
    {
    }

    UnaryOperationDelegate? conversionDelegate;

    protected override void Evaluate()
    {
        try
        {
            var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
            if (leftFault is not null)
            {
                Evaluation = (leftFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, leftFault, "{BinaryExpression} left-hand operand faulted: {Fault}", BinaryExpression, leftFault);
            }
            else
            {
                if (leftResult is not null)
                {
                    var value = conversionDelegate is null ? leftResult : conversionDelegate(leftResult);
                    Evaluation = (null, value);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{BinaryExpression} evaluated: {Value}", BinaryExpression, value);
                }
                else
                {
                    var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
                    if (rightFault is not null)
                    {
                        Evaluation = (rightFault, defaultResult);
                        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, rightFault, "{BinaryExpression} right-hand operand faulted: {Fault}", BinaryExpression, rightFault);
                    }
                    else
                    {
                        Evaluation = (null, rightResult);
                        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{BinaryExpression} evaluated: {Value}", BinaryExpression, rightResult);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{BinaryExpression} faulted: {Fault}", BinaryExpression, ex);
        }
    }

    protected override void OnInitialization()
    {
        base.OnInitialization();
        if (BinaryExpression.Conversion is { } conversion)
            conversionDelegate = conversionDelegates.GetOrAdd(conversion, ConversionDelegatesFactory);
        EvaluateIfNotDeferred();
    }
}
