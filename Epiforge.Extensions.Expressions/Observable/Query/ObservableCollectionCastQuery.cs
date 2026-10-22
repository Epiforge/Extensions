namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Casts each element of the query it is built over, keeping the results and nothing per element besides, since a cast's result is fixed by the element and nothing about an element can change it
/// </summary>
/// <remarks>
/// A cast was a projection by a lambda doing the cast, which observed every element as a projection must, though nothing that observation could hear would change what it yielded. This keeps the projection's results, faults and announcements: an element which cannot be cast is a fault of its own, reported as the projection reported it, with the result type's default in its place
/// </remarks>
sealed class ObservableCollectionCastQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source) :
    ObservableCollectionQuery<TResult>(collectionObserver),
    IObservableQueryDependent
{
    /// <summary>
    /// Casts an element as the language does, yielding the fault of a cast which cannot be made
    /// </summary>
    static TResult Cast(TElement element, out Exception? fault)
    {
        fault = null;
        if (element is TResult result)
            return result;
        try
        {
            return (TResult)(object)element!;
        }
        catch (Exception ex)
        {
            fault = ex;
            return default!;
        }
    }

    readonly object access = new();
    readonly IEqualityComparer<TElement> elementComparer = EqualityComparer<TElement>.Default;
    /// <summary>
    /// The results in order, copied for an enumeration and kept until they change, so that enumerating does not copy them again while nothing has
    /// </summary>
    TResult[]? enumerationSnapshot;
    bool released;
    readonly List<TResult> results = [];
    ObservableQuerySubscription? sourceSubscription;

    public override TResult this[int index]
    {
        get
        {
            lock (access)
            {
                if (index < 0 || index >= results.Count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                return results[index];
            }
        }
    }

    private protected override int CurrentCount
    {
        get
        {
            lock (access)
                return results.Count;
        }
    }

    internal override bool HasIndexerPenalty =>
        false;

    /// <summary>
    /// Casts an element into the results, adding its fault where it has one to a list made on the first fault, which begins with the fault the query already reports where the cast follows a change rather than starting the results over
    /// </summary>
    void CastInto(TElement element, int index, ref FaultList? faultList, Exception? standingFault)
    {
        results.Insert(index, Cast(element, out var fault));
        if (fault is not null)
            (faultList ??= new(standingFault)).Add(new EvaluationFaultException(element, fault));
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                lock (access)
                {
                    released = true;
                    source.UnsubscribeDependent(sourceSubscription!);
                    enumerationSnapshot = null;
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TResult> GetEnumerator()
    {
        lock (access)
            return ((IEnumerable<TResult>)(enumerationSnapshot ??= [.. results])).GetEnumerator();
    }

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e) =>
        SourceCollectionChanged(e);

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                if (!released)
                    InheritOperationFault(source.OperationFault);
        }
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            FaultList? faultList = null;
            CastAllWithAccess(ref faultList);
            OperationFault = faultList?.Fault;
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    void CastAllWithAccess(ref FaultList? faultList)
    {
        if (!source.HasIndexerPenalty)
        {
            var count = source.Count;
            results.Capacity = count;
            for (var i = 0; i < count; ++i)
                CastInto(source[i], i, ref faultList, null);
        }
        else
            foreach (var element in source)
                CastInto(element, results.Count, ref faultList, null);
    }

    NotifyCollectionChangedEventArgs ResetWithAccess()
    {
        results.Clear();
        enumerationSnapshot = null;
        FaultList? faultList = null;
        CastAllWithAccess(ref faultList);
        OperationFault = faultList?.Fault;
        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }

    void SourceCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            FaultList? faultList = null;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new ChangedElements<TResult>();
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > results.Count)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        for (var i = 0; i < e.OldItems.Count; ++i)
                        {
                            oldItems.Add(results[e.OldStartingIndex + i]);
                            var element = (TElement)e.OldItems[i]!;
                            Cast(element, out var fault);
                            if (fault is not null)
                                (faultList ??= new(OwnOperationFault)).RemoveElementOccurrence(element, elementComparer);
                        }
                        results.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    }
                    var newItems = new ChangedElements<TResult>();
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        for (var i = 0; i < e.NewItems.Count; ++i)
                        {
                            CastInto((TElement)e.NewItems[i]!, e.NewStartingIndex + i, ref faultList, OwnOperationFault);
                            newItems.Add(results[e.NewStartingIndex + i]);
                        }
                    if (faultList is not null)
                        OperationFault = faultList.Fault;
                    if (oldItems.Count > 0)
                        eventArgs = newItems.Count > 0 ? newItems.ToReplacementOf(oldItems, e.OldStartingIndex) : oldItems.ToRemoval(e.OldStartingIndex);
                    else if (newItems.Count > 0)
                        eventArgs = newItems.ToAddition(e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0 && e.OldStartingIndex != e.NewStartingIndex)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > results.Count)
                        {
                            eventArgs = ResetWithAccess();
                            break;
                        }
                        var movedItems = new ChangedElements<TResult>();
                        for (var i = 0; i < e.OldItems.Count; ++i)
                            movedItems.Add(results[e.OldStartingIndex + i]);
                        ListEdits.MoveRange(results, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        eventArgs = movedItems.ToMove(e.NewStartingIndex, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    eventArgs = ResetWithAccess();
                    break;
            }
            if (eventArgs is not null)
            {
                enumerationSnapshot = null;
                OnCollectionChanged(eventArgs);
            }
        }
    }

    public override string ToString() =>
        $"casting {source} to {typeof(TResult).FullName}";

    private protected override bool TryCopyTo(TResult[] array, int arrayIndex)
    {
        lock (access)
        {
            if (array.Length - arrayIndex < results.Count)
                return false;
            results.CopyTo(array, arrayIndex);
            return true;
        }
    }
}
