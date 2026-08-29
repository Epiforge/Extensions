namespace Epiforge.Extensions.Expressions.Observable;

interface IObservableExpressionDependent
{
    void OnDependencyEvaluationChanged(ObservableExpression dependency);

    /// <summary>
    /// Called when the contents of a dependency's value have changed while the value itself has not, which a dependent comparing evaluations must announce anyway
    /// </summary>
    void OnDependencyValueContentsChanged(ObservableExpression dependency) =>
        OnDependencyEvaluationChanged(dependency);
}
