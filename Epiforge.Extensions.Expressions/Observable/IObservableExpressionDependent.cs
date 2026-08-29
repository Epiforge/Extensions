namespace Epiforge.Extensions.Expressions.Observable;

interface IObservableExpressionDependent
{
    void OnDependencyEvaluationChanged(ObservableExpression dependency);

    /// <summary>
    /// Called before a dependency's evaluation is replaced, for the benefit of dependents which surface the change to a caller; nodes have no use for it
    /// </summary>
    void OnDependencyEvaluationChanging(ObservableExpression dependency)
    {
    }
}
