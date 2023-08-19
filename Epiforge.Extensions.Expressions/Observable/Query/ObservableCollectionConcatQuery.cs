namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionConcatQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionConcatQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> first, IObservableCollectionQuery<TElement> second) :
        base(collectionObserver)
    {
        this.first = first;
        Second = second;
    }

    readonly ObservableCollectionQuery<TElement> first;
    int firstCount;

    internal readonly IObservableCollectionQuery<TElement> Second;

    public override TElement this[int index] =>
        index >= firstCount ? Second[index - firstCount] : first[index];

    public override int Count =>
        firstCount + Second.Count;

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
        first.CollectionChanged += FirstCollectionChanged;
        first.PropertyChanged += FirstPropertyChanged;
        Second.CollectionChanged += SecondCollectionChanged;
        Second.PropertyChanged += SecondPropertyChanged;
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }

    void SecondCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e.Action switch
        {
            NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, firstCount + e.NewStartingIndex),
            NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, e.NewItems, firstCount + e.NewStartingIndex, firstCount + e.OldStartingIndex),
            NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, e.OldItems, firstCount + e.OldStartingIndex),
            NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, e.NewItems!, e.OldItems!, firstCount + e.NewStartingIndex),
            NotifyCollectionChangedAction.Reset => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
            _ => throw new NotSupportedException(),
        });

    void SecondPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<TElement>.OperationFault))
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
