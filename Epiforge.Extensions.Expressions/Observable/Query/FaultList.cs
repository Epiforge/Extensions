namespace Epiforge.Extensions.Expressions.Observable.Query;

class FaultList
{
    /// <summary>
    /// Determines whether two faults describe the same exceptions, which they do when they are the same exception or aggregate the same exceptions in the same order, so that a fault rebuilt around nothing new is not reported as a change
    /// </summary>
    public static bool DescribeTheSame(Exception? fault, Exception? other)
    {
        if (ReferenceEquals(fault, other))
            return true;
        if (fault is not AggregateException aggregate || other is not AggregateException otherAggregate)
            return false;
        var exceptions = aggregate.InnerExceptions;
        var otherExceptions = otherAggregate.InnerExceptions;
        if (exceptions.Count != otherExceptions.Count)
            return false;
        for (int i = 0, ii = exceptions.Count; i < ii; ++i)
            if (!ReferenceEquals(exceptions[i], otherExceptions[i]))
                return false;
        return true;
    }

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
        var exceptionGroups = ((IEnumerable<Exception>?)faultList.exceptions ?? []).ToLookup(exception => exception is EvaluationFaultException);
        var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
        if (removeExisting)
            evaluationFaults.RemoveAll(elementFault => IsFaultOf(elementFault, element, elementComparer));
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
        var exceptionGroups = ((IEnumerable<Exception>?)faultList.exceptions ?? []).ToLookup(exception => exception is EvaluationFaultException);
        var evaluationFaults = exceptionGroups[true].Cast<EvaluationFaultException>().ToList();
        if (removeExisting)
            evaluationFaults.RemoveAll(elementFault => IsFaultOf(elementFault, key, keyComparer));
        if (addNew)
            evaluationFaults.Add(new EvaluationFaultException(key, newFault!));
        faultList.Clear();
        faultList.AddRange(exceptionGroups[false]);
        faultList.AddRange(evaluationFaults);
        return faultList.Fault;
    }

    /// <summary>
    /// Determines whether a fault is the one recorded for an element, which for a null element is one recorded for null, since null matches no type pattern
    /// </summary>
    static bool IsFaultOf<TElement>(EvaluationFaultException elementFault, TElement element, IEqualityComparer<TElement> elementComparer) =>
        elementFault.Element is TElement faultElement ? elementComparer.Equals(faultElement, element) : elementFault.Element is null && element is null;

    public FaultList()
    {
    }

    public FaultList(Exception? operationFault)
    {
        if (operationFault is not null)
            Add(operationFault);
    }

    /// <summary>
    /// The exceptions listed, made with the first of them, since most lists are made to find that there are none
    /// </summary>
    List<Exception>? exceptions;

    public Exception? Fault =>
        exceptions switch
        {
            null or { Count: 0 } => null,
            { Count: 1 } => exceptions[0],
            _ => new AggregateException(exceptions)
        };

    public void Add(Exception exception)
    {
        if (exception is AggregateException aggregateException)
            foreach (var innerException in aggregateException.InnerExceptions)
                Add(innerException);
        else
            (exceptions ??= []).Add(exception);
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
        exceptions?.Clear();

    public bool RemoveElementOccurrence<TElement>(TElement element, IEqualityComparer<TElement> elementComparer)
    {
        if (exceptions is not { } listed)
            return false;
        for (int i = 0, ii = listed.Count; i < ii; ++i)
            if (listed[i] is EvaluationFaultException elementFault && IsFaultOf(elementFault, element, elementComparer))
            {
                listed.RemoveAt(i);
                return true;
            }
        return false;
    }

    public int RemoveKey<TKey>(TKey key, IEqualityComparer<TKey> keyComparer) =>
        exceptions?.RemoveAll(exception => exception is EvaluationFaultException elementFault && IsFaultOf(elementFault, key, keyComparer)) ?? 0;
}
