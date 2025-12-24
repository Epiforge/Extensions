namespace Epiforge.Extensions.Expressions.Observable.Query;

class FaultList
{
    public static bool ExchangeElementFault<TElement>(Exception? operationFault, TElement element, IEqualityComparer<TElement> elementComparer, Exception? oldFault, Exception? newFault, out Exception? newOperationFault)
    {
        var addedFault = oldFault is null && newFault is not null;
        var removedFault = oldFault is not null && newFault is null;
        var replacedFault = oldFault is not null && newFault is not null && !ReferenceEquals(oldFault, newFault);
        if (addedFault || removedFault || replacedFault)
        {
            var faultList = new FaultList();
            if (operationFault is not null)
                faultList.Add(operationFault);
            var exceptionGroups = faultList.exceptions.ToLookup(exception => exception is EvaluationFaultException);
            var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
            if (removedFault || replacedFault)
                evaluationFaults.RemoveAll(elementFault => elementFault.Element is TElement faultElement && elementComparer.Equals(faultElement, element));
            if (addedFault || replacedFault)
                evaluationFaults.Add(new EvaluationFaultException(element, newFault!));
            faultList.Clear();
            faultList.AddRange(exceptionGroups[false]);
            faultList.AddRange(evaluationFaults);
            newOperationFault = faultList.Fault;
            return true;
        }
        newOperationFault = operationFault;
        return false;
    }

    public static bool ExchangeKeyFault<TKey>(Exception? operationFault, TKey key, IEqualityComparer<TKey> keyComparer, Exception? oldFault, Exception? newFault, out Exception? newOperationFault)
    {
        var addedFault = oldFault is null && newFault is not null;
        var removedFault = oldFault is not null && newFault is null;
        var replacedFault = oldFault is not null && newFault is not null && !ReferenceEquals(oldFault, newFault);
        if (addedFault || removedFault || replacedFault)
        {
            var faultList = new FaultList();
            if (operationFault is not null)
                faultList.Add(operationFault);
            var exceptionGroups = faultList.exceptions.ToLookup(exception => exception is EvaluationFaultException);
            var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
            if (removedFault || replacedFault)
                evaluationFaults.RemoveAll(elementFault => elementFault.Element is TKey faultKey && keyComparer.Equals(faultKey, key));
            if (addedFault || replacedFault)
                evaluationFaults.Add(new EvaluationFaultException(key, newFault!));
            faultList.Clear();
            faultList.AddRange(exceptionGroups[false]);
            faultList.AddRange(evaluationFaults);
            newOperationFault = faultList.Fault;
            return true;
        }
        newOperationFault = operationFault;
        return false;
    }

    public FaultList()
    {
    }

    public FaultList(Exception? operationFault)
    {
        if (operationFault is not null)
            Add(operationFault);
    }

    readonly List<Exception> exceptions = [];

    public Exception? Fault =>
        exceptions.Count switch
        {
            0 => null,
            1 => exceptions[0],
            _ => new AggregateException(exceptions)
        };

    public void Add(Exception exception)
    {
        if (exception is AggregateException aggregateException)
            foreach (var innerException in aggregateException.InnerExceptions)
                Add(innerException);
        else
            exceptions.Add(exception);
    }

    public void AddRange(IEnumerable<Exception> exceptions)
    {
        foreach (var exception in exceptions)
            Add(exception);
    }

    public bool Check<TElement, TResult>(IObservableExpression<TElement, TResult> observableExpression)
    {
        if (observableExpression.Evaluation.Fault is { } fault)
        {
            Add(new EvaluationFaultException(observableExpression.Argument, fault));
            return true;
        }
        return false;
    }

    public bool Check<TKey, TValue, TEvaluation>(IObservableExpression<KeyValuePair<TKey, TValue>, TEvaluation> observableExpression)
    {
        if (observableExpression.Evaluation.Fault is { } fault)
        {
            Add(new EvaluationFaultException(observableExpression.Argument.Key, fault));
            return true;
        }
        return false;
    }

    public bool Check<TElement>(IObservableCollectionQuery<TElement> query)
    {
        if (query.OperationFault is { } fault)
        {
            Add(fault);
            return true;
        }
        return false;
    }

    public bool Check<TKey, TValue>(IObservableDictionaryQuery<TKey, TValue> query)
        where TKey : notnull
    {
        if (query.OperationFault is { } fault)
        {
            Add(fault);
            return true;
        }
        return false;
    }

    public bool Check<TValue>(IObservableScalarQuery<TValue> query)
    {
        if (query.Evaluation.Fault is { } fault)
        {
            Add(fault);
            return true;
        }
        return false;
    }

    public void Clear() =>
        exceptions.Clear();

    public int RemoveElement<TElement>(TElement element, IEqualityComparer<TElement> elementComparer) =>
        exceptions.RemoveAll(exception => exception is EvaluationFaultException elementFault && elementFault.Element is TElement faultElement && elementComparer.Equals(faultElement, element));

    public int RemoveKey<TKey>(TKey key, IEqualityComparer<TKey> keyComparer) =>
        exceptions.RemoveAll(exception => exception is EvaluationFaultException elementFault && elementFault.Element is TKey faultKey && keyComparer.Equals(faultKey, key));
}
