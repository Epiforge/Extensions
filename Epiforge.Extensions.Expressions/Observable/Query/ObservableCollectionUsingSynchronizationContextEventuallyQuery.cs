namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionUsingSynchronizationContextEventuallyQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, SynchronizationContext synchronizationContext) :
        base(collectionObserver)
    {
        this.source = source;
        SynchronizationContext = synchronizationContext;
    }

    RangeObservableCollection<TElement>? elements;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly SynchronizationContext SynchronizationContext;

    public override TElement this[int index] =>
        SynchronizationContext.Send(() => elements![index]);

    public override int Count =>
        SynchronizationContext.Send(() => elements!.Count);

    public override bool IsSynchronized =>
        true;

    public override Exception? OperationFault
    {
        get => SynchronizationContext.Send(() => source.OperationFault);
        protected set => throw new NotImplementedException();
    }

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
        SynchronizationContext.Send(() => elements!.ToList().AsReadOnly().GetEnumerator());

    protected override void OnInitialization()
    {
        elements = new(source);
        source.CollectionChanged += SourceCollectionChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SynchronizationContext.Post(_ =>
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
        }, null);

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext} eventually";
}
