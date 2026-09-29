namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Observes the key of each distinct element of a source, once however many times the element occurs, and tells its owner as occurrences take and give up keys, leaving an element out while its key faults
/// </summary>
/// <remarks>
/// A grouping or a lookup reads only which key each element has, where a projection pairing every occurrence with its key keeps its results in source order, allocates a pair for every evaluation and raises a replacement for every occurrence of an element whose key changes. A reference type is told apart by identity, as that projection's observations were, so that an element equal to another but distinct from it keeps a key of its own. Every member is called with the owner's lock held, and a key is subscribed to before it is read and read only once its element is recorded, so that a change made on another thread in between waits on that lock and then finds itself already taken up rather than going unheard
/// </remarks>
sealed class ObservedElementKeys<TElement, TKey>(ObservedElementKeys<TElement, TKey>.IOwner owner, IExpressionObserver expressionObserver, Expression<Func<TElement, TKey>> keySelector)
{
    internal interface IOwner
    {
        PropertyChangedEventHandler KeyEvaluationChangedHandler { get; }

        void ElementGainedKey(TElement element, TKey key);

        void ElementLostKey(TElement element, TKey key, bool isOnlyOccurrence);
    }

    sealed class Entry(IObservableExpression<TElement, TKey> observation)
    {
        internal Exception? Fault;
        internal TKey Key = default!;
        internal readonly IObservableExpression<TElement, TKey> Observation = observation;
        internal int Occurrences;
    }

    static readonly IEqualityComparer<TElement> elementComparer = typeof(TElement).IsValueType ? EqualityComparer<TElement>.Default : (IEqualityComparer<TElement>)(object)ReferenceEqualityComparer.Instance;

    readonly NullableKeyDictionary<TElement, Entry> entries = new(elementComparer);
    readonly FaultList faults = new();
    bool faultsChanged;

    internal void Add(TElement element)
    {
        if (!entries.TryGetValue(element, out var entry))
        {
            var observation = expressionObserver.ObserveWithoutOptimization(keySelector, element);
            observation.PropertyChanged += owner.KeyEvaluationChangedHandler;
            entry = new(observation);
            entries.Add(element, entry);
            (entry.Fault, entry.Key) = observation.Evaluation;
            if (entry.Fault is { } fault)
            {
                faults.Add(new EvaluationFaultException(element, fault));
                faultsChanged = true;
            }
        }
        ++entry.Occurrences;
        if (entry.Fault is null)
            owner.ElementGainedKey(element, entry.Key);
    }

    /// <summary>
    /// Stops observing every element without telling the owner, which is how the owner starts over or stops altogether
    /// </summary>
    internal void Clear()
    {
        foreach (var entry in entries.Values)
        {
            entry.Observation.PropertyChanged -= owner.KeyEvaluationChangedHandler;
            entry.Observation.Dispose();
        }
        entries.Clear();
        faults.Clear();
        faultsChanged = true;
    }

    internal void EvaluationChanged(IObservableExpression<TElement, TKey> observation)
    {
        var element = observation.Argument;
        if (!entries.TryGetValue(element, out var entry) || !ReferenceEquals(entry.Observation, observation))
            return;
        var (fault, key) = observation.Evaluation;
        var oldFault = entry.Fault;
        var oldKey = entry.Key;
        if (!ReferenceEquals(oldFault, fault))
        {
            if (oldFault is not null)
                faults.RemoveElementOccurrence(element, elementComparer);
            if (fault is not null)
                faults.Add(new EvaluationFaultException(element, fault));
            faultsChanged = true;
        }
        else if (fault is not null || EqualityComparer<TKey>.Default.Equals(oldKey, key))
            return;
        entry.Fault = fault;
        entry.Key = key;
        if (oldFault is null && fault is null)
            for (var i = 0; i < entry.Occurrences; ++i)
            {
                owner.ElementLostKey(element, oldKey, entry.Occurrences == 1);
                owner.ElementGainedKey(element, key);
            }
        else if (oldFault is null)
            for (var i = 0; i < entry.Occurrences; ++i)
                owner.ElementLostKey(element, oldKey, entry.Occurrences == 1);
        else if (fault is null)
            for (var i = 0; i < entry.Occurrences; ++i)
                owner.ElementGainedKey(element, key);
    }

    internal void Remove(TElement element)
    {
        if (!entries.TryGetValue(element, out var entry))
            return;
        if (entry.Fault is null)
            owner.ElementLostKey(element, entry.Key, entry.Occurrences == 1);
        if (--entry.Occurrences > 0)
            return;
        entries.Remove(element);
        entry.Observation.PropertyChanged -= owner.KeyEvaluationChangedHandler;
        entry.Observation.Dispose();
        if (entry.Fault is not null)
        {
            faults.RemoveElementOccurrence(element, elementComparer);
            faultsChanged = true;
        }
    }

    /// <summary>
    /// Takes the fault of the keys observed where it has changed since it was last taken
    /// </summary>
    /// <returns><c>true</c> if the fault has changed; otherwise, <c>false</c></returns>
    internal bool TryTakeFault(out Exception? fault)
    {
        if (!faultsChanged)
        {
            fault = null;
            return false;
        }
        faultsChanged = false;
        fault = faults.Fault;
        return true;
    }
}
