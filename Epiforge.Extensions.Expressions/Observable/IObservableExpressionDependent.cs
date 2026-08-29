namespace Epiforge.Extensions.Expressions.Observable;

interface IObservableExpressionDependent
{
    void OnDependencyEvaluationChanged(ObservableExpression dependency);
}
