namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableExpressionSubscription
{
    internal ObservableExpressionSubscription(IObservableExpressionDependent dependent) =>
        Dependent = dependent;

    internal readonly IObservableExpressionDependent Dependent;
    internal volatile bool IsRemoved;
    internal volatile ObservableExpressionSubscription? Next;
    internal ObservableExpressionSubscription? Previous;
}
