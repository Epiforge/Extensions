namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionIndividualChangesQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionIndividualChangesQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source) :
        base(collectionObserver)
    {
        access = new();
        elements = Logger is null ? new() : new(Logger);
        this.source = source;
    }

    readonly object access;
    IReadOnlyList<TElement>? enumerationSnapshot;
    readonly ObservableRangeCollection<TElement> elements;
    /// <summary>
    /// Whether this query is following a change of its source, during which it delivers a notification for each step it takes, so that a change its subscribers make to the source in the meantime waits until this one has been followed
    /// </summary>
    bool followingSourceChange;
    /// <summary>
    /// The changes the source announced while this query was following another, in the order they were announced, made with the first of them
    /// </summary>
    Queue<NotifyCollectionChangedEventArgs>? pendingSourceChanges;
    readonly ObservableCollectionQuery<TElement> source;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
                return elements[index];
        }
    }

    private protected override int CountAtInitialization =>
        elements.Count;

    private protected override int CurrentCount
    {
        get
        {
            lock (access)
                return elements.Count;
        }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                elements.CollectionChanged -= ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements).PropertyChanged -= ElementsPropertyChanged;
                source.CollectionChanged -= SourceCollectionChanged;
                source.PropertyChanged -= SourcePropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
            return (enumerationSnapshot ??= elements.ToList().AsReadOnly()).GetEnumerator();
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        elements.Reset(source);
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
        InheritOperationFault(source.OperationFault);
        source.CollectionChanged += SourceCollectionChanged;
        source.PropertyChanged += SourcePropertyChanged;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        enumerationSnapshot = null;
        OnCollectionChanged(e);
    }

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                InheritOperationFault(source.OperationFault);
        }
    }

    /// <summary>
    /// Follows a change of the source one element at a time, delivering the notification of each step before taking the next, so that a subscriber finds this query as each notification leaves it
    /// </summary>
    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    void FollowSourceChange(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset)
            lock (access)
                elements.Reset(source);
        else if (e.Action is NotifyCollectionChangedAction.Move)
        {
            var count = e.OldItems?.Count ?? 0;
            if (e.OldStartingIndex != e.NewStartingIndex && count > 0)
            {
                var extractionIndex = e.OldStartingIndex;
                var insertionIndex = e.NewStartingIndex - 1;
                for (var i = 0; i < count; ++i)
                {
                    lock (access)
                        elements.Move(extractionIndex, ++insertionIndex);
                    RaiseNotificationsDeferredSoFar();
                    if (e.OldStartingIndex > e.NewStartingIndex)
                        ++extractionIndex;
                }
            }
        }
        else if (e.Action is NotifyCollectionChangedAction.Replace && e.OldItems is { } replaceOldItems && replaceOldItems.Count > 0 && e.NewItems is { } replaceNewItems && replaceNewItems.Count > 0 && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex)
        {
            var index = e.OldStartingIndex;
            for (int i = 0, ii = Math.Max(replaceOldItems.Count, replaceNewItems.Count); i < ii; ++i)
            {
                lock (access)
                {
                    if (i >= replaceOldItems.Count)
                        elements.Insert(index + i, (TElement)replaceNewItems[i]!);
                    else if (i >= replaceNewItems.Count)
                        elements.RemoveAt(replaceNewItems.Count + index);
                    else
                        elements[index + i] = (TElement)replaceNewItems[i]!;
                }
                RaiseNotificationsDeferredSoFar();
            }
        }
        else
        {
            if (e.OldItems is { } oldItems && e.OldStartingIndex >= 0)
            {
                var count = oldItems.Count;
                if (count > 0)
                    for (var i = 0; i < count; ++i)
                    {
                        lock (access)
                            elements.RemoveAt(e.OldStartingIndex);
                        RaiseNotificationsDeferredSoFar();
                    }
            }
            if (e.NewItems is { } newItems && e.NewStartingIndex >= 0)
            {
                var count = newItems.Count;
                if (count > 0)
                    for (var i = 0; i < count; ++i)
                    {
                        lock (access)
                            elements.Insert(e.NewStartingIndex + i, (TElement)newItems[i]!);
                        RaiseNotificationsDeferredSoFar();
                    }
            }
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (followingSourceChange)
        {
            (pendingSourceChanges ??= new()).Enqueue(e);
            return;
        }
        followingSourceChange = true;
        try
        {
            FollowSourceChange(e);
            while (pendingSourceChanges is { Count: > 0 } pending)
                FollowSourceChange(pending.Dequeue());
        }
        finally
        {
            followingSourceChange = false;
        }
    }

    public override string ToString() =>
        $"individual changes to {source}";
}
