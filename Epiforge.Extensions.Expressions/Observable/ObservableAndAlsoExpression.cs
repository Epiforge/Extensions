namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableAndAlsoExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
    ObservableBinaryExpression(observer, binaryExpression, deferEvaluation)
{
    protected override void Evaluate()
    {
        var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
        if (leftFault is not null)
        {
            Evaluation = (leftFault, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, leftFault, "{BinaryExpression} left-hand operand faulted: {Fault}", BinaryExpression, leftFault);
        }
        else if (!(leftResult is bool leftBool && leftBool))
        {
            Evaluation = (null, false);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{BinaryExpression} evaluated: {Value}", BinaryExpression, false);
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
                var value = rightResult is bool rightBool && rightBool;
                Evaluation = (null, value);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{BinaryExpression} evaluated: {Value}", BinaryExpression, value);
            }
        }
    }
}
