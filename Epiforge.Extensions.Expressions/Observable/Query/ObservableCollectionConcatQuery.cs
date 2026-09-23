namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionConcatQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> first, IObservableCollectionQuery<TElement> second) :
    ObservableCollectionQuery<TElement>(collectionObserver),
    IObservableQueryDependent
{
#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    int count;
    int firstCount;
    ObservableQuerySubscription? firstSubscription;
    int secondCount;
    ObservableQuerySubscription? secondSubscription;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    internal readonly IObservableCollectionQuery<TElement> Second = second;

    public override TElement this[int index]
    {
        get
        {
            int offset;
            lock (access)
                offset = firstCount;
            return index >= offset ? Second[index - offset] : first[index];
        }
    }

    public override int Count =>
        count;

    internal override bool HasIndexerPenalty =>
        true;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = first.QueryDisposed(this);
            if (removedFromCache)
            {
                first.UnsubscribeDependent(firstSubscription!);
                if (secondSubscription is { } subscription)
                    ((ObservableQuery)Second).UnsubscribeDependent(subscription);
                else
                {
                    Second.CollectionChanged -= SecondCollectionChanged;
                    Second.PropertyChanged -= SecondPropertyChanged;
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void FirstCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
                firstCount = first.Count;
            else
                firstCount += (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
            if (e.Action is not NotifyCollectionChangedAction.Move)
                SetCount();
            OnCollectionChanged(e);
        }
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        first.Concat(Second).GetEnumerator();

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (subscription == firstSubscription)
                FirstCollectionChanged(e);
            else
                SecondCollectionChanged(e);
        }
    }

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            SetOperationFault();
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            firstCount = first.Count;
            secondCount = Second.Count;
            count = firstCount + secondCount;
            firstSubscription = first.SubscribeDependent(this);
            if (Second is ObservableQuery secondQuery)
                secondSubscription = secondQuery.SubscribeDependent(this);
            else
            {
                Second.CollectionChanged += SecondCollectionChanged;
                Second.PropertyChanged += SecondPropertyChanged;
            }
        }
    }

    void SecondCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SecondCollectionChanged(e);

    void SecondCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
                secondCount = Second.Count;
            else
                secondCount += (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
            if (e.Action is not NotifyCollectionChangedAction.Move)
                SetCount();
            OnCollectionChanged(e.Action switch
            {
                NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, firstCount + e.NewStartingIndex),
                NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, e.NewItems, firstCount + e.NewStartingIndex, firstCount + e.OldStartingIndex),
                NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, e.OldItems, firstCount + e.OldStartingIndex),
                NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, e.NewItems!, e.OldItems!, firstCount + e.NewStartingIndex),
                NotifyCollectionChangedAction.Reset => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
                _ => throw new NotSupportedException($"collection changed action {e.Action} is not supported"),
            });
        }
    }

    void SecondPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
    }

    void SetCount()
    {
        var value = firstCount + secondCount;
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);
    }

    void SetOperationFault()
    {
        var faultList = new FaultList();
        faultList.Check(first);
        faultList.Check(Second);
        OperationFault = faultList.Fault;
    }

    public override string ToString() =>
        $"concatenation of {first} and {Second}";
}
