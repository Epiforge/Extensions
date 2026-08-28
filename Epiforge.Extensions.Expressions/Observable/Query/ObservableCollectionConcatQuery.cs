namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionConcatQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> first, IObservableCollectionQuery<TElement> second) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    int count;
    int firstCount;
    int secondCount;

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

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = first.QueryDisposed(this);
            if (removedFromCache)
            {
                first.CollectionChanged -= FirstCollectionChanged;
                first.PropertyChanged -= FirstPropertyChanged;
                Second.CollectionChanged -= SecondCollectionChanged;
                Second.PropertyChanged -= SecondPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void FirstCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

    void FirstPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            SetOperationFault();
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        first.Concat(Second).GetEnumerator();

    protected override void OnInitialization()
    {
        lock (access)
        {
            firstCount = first.Count;
            secondCount = Second.Count;
            count = firstCount + secondCount;
            first.CollectionChanged += FirstCollectionChanged;
            first.PropertyChanged += FirstPropertyChanged;
            Second.CollectionChanged += SecondCollectionChanged;
            Second.PropertyChanged += SecondPropertyChanged;
        }
    }

    void SecondCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
