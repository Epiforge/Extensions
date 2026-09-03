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
            newOperationFault = NewOperationFaultForElement(operationFault, element, elementComparer, removedFault || replacedFault, addedFault || replacedFault, newFault);
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
            newOperationFault = NewOperationFaultForKey(operationFault, key, keyComparer, removedFault || replacedFault, addedFault || replacedFault, newFault);
            return true;
        }
        newOperationFault = operationFault;
        return false;
    }

    /// <summary>
    /// Rebuilds the operation fault around a change to one element's fault, in a method of its own because the lambda below captures the element and its comparer, and a captured parameter is copied into a closure the moment its method is entered, whether or not the branch which uses it is taken
    /// </summary>
    static Exception? NewOperationFaultForElement<TElement>(Exception? operationFault, TElement element, IEqualityComparer<TElement> elementComparer, bool removeExisting, bool addNew, Exception? newFault)
    {
        var faultList = new FaultList();
        if (operationFault is not null)
            faultList.Add(operationFault);
        var exceptionGroups = faultList.exceptions.ToLookup(exception => exception is EvaluationFaultException);
        var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
        if (removeExisting)
            evaluationFaults.RemoveAll(elementFault => elementFault.Element is TElement faultElement && elementComparer.Equals(faultElement, element));
        if (addNew)
            evaluationFaults.Add(new EvaluationFaultException(element, newFault!));
        faultList.Clear();
        faultList.AddRange(exceptionGroups[false]);
        faultList.AddRange(evaluationFaults);
        return faultList.Fault;
    }

    /// <summary>
    /// Rebuilds the operation fault around a change to one key's fault, separated from its caller for the reason given on <see cref="NewOperationFaultForElement"/>
    /// </summary>
    static Exception? NewOperationFaultForKey<TKey>(Exception? operationFault, TKey key, IEqualityComparer<TKey> keyComparer, bool removeExisting, bool addNew, Exception? newFault)
    {
        var faultList = new FaultList();
        if (operationFault is not null)
            faultList.Add(operationFault);
        var exceptionGroups = faultList.exceptions.ToLookup(exception => exception is EvaluationFaultException);
        var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
        if (removeExisting)
            evaluationFaults.RemoveAll(elementFault => elementFault.Element is TKey faultKey && keyComparer.Equals(faultKey, key));
        if (addNew)
            evaluationFaults.Add(new EvaluationFaultException(key, newFault!));
        faultList.Clear();
        faultList.AddRange(exceptionGroups[false]);
        faultList.AddRange(evaluationFaults);
        return faultList.Fault;
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

    public bool RemoveElementOccurrence<TElement>(TElement element, IEqualityComparer<TElement> elementComparer)
    {
        for (int i = 0, ii = exceptions.Count; i < ii; ++i)
            if (exceptions[i] is EvaluationFaultException elementFault && elementFault.Element is TElement faultElement && elementComparer.Equals(faultElement, element))
            {
                exceptions.RemoveAt(i);
                return true;
            }
        return false;
    }

    public int RemoveKey<TKey>(TKey key, IEqualityComparer<TKey> keyComparer) =>
        exceptions.RemoveAll(exception => exception is EvaluationFaultException elementFault && elementFault.Element is TKey faultKey && keyComparer.Equals(faultKey, key));
}
