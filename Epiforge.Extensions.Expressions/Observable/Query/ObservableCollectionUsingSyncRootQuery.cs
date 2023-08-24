namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSyncRootQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionUsingSyncRootQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, object syncRoot) :
        base(collectionObserver)
    {
        this.source = source;
        SyncRoot = syncRoot;
    }

    ObservableRangeCollection<TElement>? elements;
    readonly ObservableCollectionQuery<TElement> source;

    public override TElement this[int index] =>
        elements![index];

    public override int Count =>
        elements!.Count;

    public override Exception? OperationFault
    {
        get => source.OperationFault;
        protected set => throw new NotImplementedException();
    }

    public override object SyncRoot { get; }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.CollectionChanged -= SourceCollectionChanged;
                elements!.CollectionChanged += ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements!).PropertyChanged -= ElementsPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        elements!.GetEnumerator();

    protected override void OnInitialization()
    {
        elements = new(source);
        source.CollectionChanged += SourceCollectionChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (SyncRoot!)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    elements!.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<TElement>());
                    break;
                case NotifyCollectionChangedAction.Move:
                    elements!.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    elements!.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    elements!.ReplaceRange(e.OldStartingIndex, e.OldItems!.Count, e.NewItems!.Cast<TElement>());
                    break;
                case NotifyCollectionChangedAction.Reset:
                    elements!.Reset(source);
                    break;
            }
        }
    }

    public override string ToString() =>
        $"synchronizing {source} using {SyncRoot} eventually";
}
