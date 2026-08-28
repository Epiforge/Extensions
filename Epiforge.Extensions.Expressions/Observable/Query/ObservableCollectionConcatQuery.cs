namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionConcatQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> first, IObservableCollectionQuery<TElement> second) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    int announcedCount;
    int firstCount;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    internal readonly IObservableCollectionQuery<TElement> Second = second;

    public override TElement this[int index]
    {
        get
        {
            var offset = firstCount;
            return index >= offset ? Second[index - offset] : first[index];
        }
    }

    public override int Count =>
        firstCount + Second.Count;

    void AnnounceCount()
    {
        var value = firstCount + Second.Count;
        if (Interlocked.Exchange(ref announcedCount, value) == value)
            return;
        OnPropertyChanging(countPropertyChangingEventArgs);
        OnPropertyChanged(countPropertyChangedEventArgs);
    }

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
        if (e.Action == NotifyCollectionChangedAction.Reset)
            firstCount = first.Count;
        else
            firstCount += (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        if (e.Action != NotifyCollectionChangedAction.Move)
            AnnounceCount();
        OnCollectionChanged(e);
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
        firstCount = first.Count;
        announcedCount = firstCount + Second.Count;
        first.CollectionChanged += FirstCollectionChanged;
        first.PropertyChanged += FirstPropertyChanged;
        Second.CollectionChanged += SecondCollectionChanged;
        Second.PropertyChanged += SecondPropertyChanged;
    }

    void SecondCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Move)
            AnnounceCount();
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

    void SecondPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
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
