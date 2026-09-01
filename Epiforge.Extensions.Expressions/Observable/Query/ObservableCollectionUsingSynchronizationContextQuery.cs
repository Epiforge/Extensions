namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationContextQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, SynchronizationContext synchronizationContext) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    ObservableRangeCollection<TElement>? elements;
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

    public override TElement this[int index] =>
        SynchronizationContext.Send(() => elements![index]);

    public override int Count =>
        SynchronizationContext.Send(() => elements!.Count);

    internal override bool HasIndexerPenalty =>
        true;

    public override bool IsSynchronized =>
        true;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.CollectionChanged -= SourceCollectionChanged;
                source.PropertyChanged -= SourcePropertyChanged;
                elements!.CollectionChanged -= ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements!).PropertyChanged -= ElementsPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        SynchronizationContext.Send(() => (IEnumerator<TElement>)elements!.ToList().GetEnumerator());

    protected override void OnInitialization()
    {
        elements = new(source);
        source.CollectionChanged += SourceCollectionChanged;
        source.PropertyChanged += SourcePropertyChanged;
        OperationFault = source.OperationFault;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void handleEventArgs()
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
        if (SynchronizationContext == SynchronizationContext.Current)
            handleEventArgs();
        else
            SynchronizationContext.Send(handleEventArgs);
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            SynchronizationContext.Send(() => OperationFault = source.OperationFault);
    }

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext}";
}
