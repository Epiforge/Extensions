namespace Epiforge.Extensions.Expressions.Observable;

interface IObservableExpressionDependent
{
    /// <summary>
    /// Gets whether this dependent needs to be told before a dependency's evaluation is replaced, which spares a dependency the walk when none of its dependents do
    /// </summary>
    bool ObservesEvaluationChanging =>
        false;

    void OnDependencyEvaluationChanged(ObservableExpression dependency);

    /// <summary>
    /// Called before a dependency's evaluation is replaced, for the benefit of dependents which surface the change to a caller; nodes have no use for it
    /// </summary>
    void OnDependencyEvaluationChanging(ObservableExpression dependency)
    {
    }
}
