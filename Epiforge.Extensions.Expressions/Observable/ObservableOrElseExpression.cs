namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableOrElseExpression :
    ObservableBinaryExpression
{
    public ObservableOrElseExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
        base(observer, binaryExpression, deferEvaluation)
    {
    }

    protected override void Evaluate()
    {
        var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
        if (leftFault is not null)
            Evaluation = (leftFault, defaultResult);
        else if (leftResult is bool leftBool && leftBool)
            Evaluation = (null, true);
        else
        {
            var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
            if (rightFault is not null)
                Evaluation = (rightFault, defaultResult);
            else
                Evaluation = (null, rightResult is bool rightBool && rightBool);
        }
    }
}
