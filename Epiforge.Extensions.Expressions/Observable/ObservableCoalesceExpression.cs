namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableCoalesceExpression :
    ObservableBinaryExpression
{
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
                Evaluation = (leftFault, defaultResult);
            else
            {
                if (leftResult is not null)
                    Evaluation = (null, conversionDelegate is null ? leftResult : conversionDelegate(leftResult));
                else
                {
                    var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
                    if (rightFault is not null)
                        Evaluation = (rightFault, defaultResult);
                    else
                        Evaluation = (null, rightResult);
                }
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
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
