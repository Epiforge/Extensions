namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Keeps the elements of the query it is built over which are of a type, keeping which of them are and nothing per element besides, since whether an element is of a type is fixed by the element and nothing about an element can change it
/// </summary>
/// <remarks>
/// A type filter was a filter by a lambda testing the type followed by a cast, which observed every element as a filter must, though nothing that observation could hear would change what it yielded. This keeps the filter's results and announcements: a test of type cannot fault, so the only fault reported is the source's
/// </remarks>
sealed class ObservableCollectionOfTypeQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source) :
    ObservableCollectionQuery<TResult>(collectionObserver),
    IObservableQueryDependent
{
    readonly object access = new();
    int count;
    /// <summary>
    /// The results in order, copied for an enumeration and kept until they change, so that enumerating does not copy them again while nothing has
    /// </summary>
    TResult[]? enumerationSnapshot;
    /// <summary>
    /// Whether each element of the source, in the source's order, is of the type, so that a position in the source can be translated to one in the results
    /// </summary>
    readonly List<bool> memberships = [];
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

    public override int Count =>
        count;

    internal override bool HasIndexerPenalty =>
        false;

    /// <summary>
    /// Counts the elements of the source before a position which are of the type, which is the position in the results the source's position corresponds to
    /// </summary>
    int CountMembersBeforeWithAccess(int index)
    {
        var span = CollectionsMarshal.AsSpan(memberships)[..index];
#if IS_NET_8_0_OR_GREATER
        return span.Count(true);
#else
        var members = 0;
        for (var i = 0; i < span.Length; ++i)
            if (span[i])
                ++members;
        return members;
#endif
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

    void FilterAllWithAccess()
    {
        if (!source.HasIndexerPenalty)
        {
            var sourceCount = source.Count;
            memberships.Capacity = sourceCount;
            for (var i = 0; i < sourceCount; ++i)
                FilterOntoEnd(source[i]);
        }
        else
            foreach (var element in source)
                FilterOntoEnd(element);
    }

    void FilterOntoEnd(TElement element)
    {
        if (element is TResult result)
        {
            memberships.Add(true);
            results.Add(result);
        }
        else
            memberships.Add(false);
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
            FilterAllWithAccess();
            count = results.Count;
            InheritOperationFault(source.OperationFault);
            sourceSubscription = source.SubscribeDependent(this);
        }
    }

    void SetCount(int value) =>
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);

    void SourceCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (released)
                return;
            NotifyCollectionChangedEventArgs? eventArgs = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                    var oldItems = new ChangedElements<TResult>();
                    var oldStartingIndex = 0;
                    if (e.OldItems is not null && e.OldStartingIndex >= 0)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > memberships.Count)
                        {
                            eventArgs = StartOverWithAccess();
                            break;
                        }
                        oldStartingIndex = CountMembersBeforeWithAccess(e.OldStartingIndex);
                        for (var i = 0; i < e.OldItems.Count; ++i)
                            if (memberships[e.OldStartingIndex + i])
                                oldItems.Add(results[oldStartingIndex + oldItems.Count]);
                        memberships.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        results.RemoveRange(oldStartingIndex, oldItems.Count);
                    }
                    var newItems = new ChangedElements<TResult>();
                    var newStartingIndex = 0;
                    if (e.NewItems is not null && e.NewStartingIndex >= 0)
                    {
                        if (e.NewStartingIndex > memberships.Count)
                        {
                            eventArgs = StartOverWithAccess();
                            break;
                        }
                        newStartingIndex = CountMembersBeforeWithAccess(e.NewStartingIndex);
                        if (e.NewItems.Count == 1)
                        {
                            if (e.NewItems[0] is TResult result)
                            {
                                memberships.Insert(e.NewStartingIndex, true);
                                newItems.Add(result);
                            }
                            else
                                memberships.Insert(e.NewStartingIndex, false);
                        }
                        else
                        {
                            var newMemberships = new bool[e.NewItems.Count];
                            for (var i = 0; i < newMemberships.Length; ++i)
                                if (e.NewItems[i] is TResult result)
                                {
                                    newMemberships[i] = true;
                                    newItems.Add(result);
                                }
                            memberships.InsertRange(e.NewStartingIndex, newMemberships);
                        }
                        if (newItems.Count > 0)
                            newItems.InsertInto(results, newStartingIndex);
                    }
                    if (newItems.Count > 0)
                        eventArgs = oldItems.Count > 0 ? newItems.ToReplacementOf(oldItems, newStartingIndex) : newItems.ToAddition(newStartingIndex);
                    else if (oldItems.Count > 0)
                        eventArgs = oldItems.ToRemoval(oldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is not null && e.OldItems.Count > 0 && e.OldStartingIndex != e.NewStartingIndex)
                    {
                        if (e.OldStartingIndex + e.OldItems.Count > memberships.Count || e.NewStartingIndex + e.OldItems.Count > memberships.Count)
                        {
                            eventArgs = StartOverWithAccess();
                            break;
                        }
                        var movedStartingIndex = CountMembersBeforeWithAccess(e.OldStartingIndex);
                        var movedItems = new ChangedElements<TResult>();
                        for (var i = 0; i < e.OldItems.Count; ++i)
                            if (memberships[e.OldStartingIndex + i])
                                movedItems.Add(results[movedStartingIndex + movedItems.Count]);
                        ListEdits.MoveRange(memberships, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        if (movedItems.Count > 0)
                        {
                            var movedToIndex = CountMembersBeforeWithAccess(e.NewStartingIndex);
                            if (movedToIndex != movedStartingIndex)
                            {
                                ListEdits.MoveRange(results, movedStartingIndex, movedToIndex, movedItems.Count);
                                eventArgs = movedItems.ToMove(movedToIndex, movedStartingIndex);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    eventArgs = StartOverWithAccess();
                    break;
            }
            if (eventArgs is not null)
            {
                enumerationSnapshot = null;
                if (eventArgs.Action != NotifyCollectionChangedAction.Move)
                    SetCount(results.Count);
                OnCollectionChanged(eventArgs);
            }
        }
    }

    NotifyCollectionChangedEventArgs StartOverWithAccess()
    {
        memberships.Clear();
        results.Clear();
        FilterAllWithAccess();
        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }

    public override string ToString() =>
        $"{source} of type {typeof(TResult).FullName}";

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
