namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableQuerySubscription
{
    internal ObservableQuerySubscription(IObservableQueryDependent dependent, int sequence)
    {
        Dependent = dependent;
        Sequence = sequence;
    }

    internal readonly IObservableQueryDependent Dependent;
    internal volatile bool IsRemoved;
    internal volatile ObservableQuerySubscription? Next;
    internal ObservableQuerySubscription? Previous;
    internal readonly int Sequence;
}
