namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableAndAlsoExpression :
    ObservableBinaryExpression
{
    public ObservableAndAlsoExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
        base(observer, binaryExpression, deferEvaluation)
    {
    }

    protected override void Evaluate()
    {
        var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
        if (leftFault is not null)
        {
            Evaluation = (leftFault, defaultResult);
            observer.Logger?.LogTrace("{BinaryExpression} left-hand operand faulted: {Fault}", BinaryExpression, leftFault);
        }
        else if (!(leftResult is bool leftBool && leftBool))
        {
            Evaluation = (null, false);
            observer.Logger?.LogTrace("{BinaryExpression} evaluated: {Value}", BinaryExpression, false);
        }
        else
        {
            var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
            if (rightFault is not null)
            {
                Evaluation = (rightFault, defaultResult);
                observer.Logger?.LogTrace("{BinaryExpression} right-hand operand faulted: {Fault}", BinaryExpression, rightFault);
            }
            else
            {
                var value = rightResult is bool rightBool && rightBool;
                Evaluation = (null, value);
                observer.Logger?.LogTrace("{BinaryExpression} evaluated: {Value}", BinaryExpression, value);
            }
        }
    }
}
