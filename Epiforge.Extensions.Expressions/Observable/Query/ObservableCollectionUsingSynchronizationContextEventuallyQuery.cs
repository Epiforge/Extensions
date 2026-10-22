namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, SynchronizationContext synchronizationContext) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    ObservableRangeCollection<TElement>? elements;
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

    public override TElement this[int index] =>
        ReadOnContext(SynchronizationContext, () => elements![index]);

    private protected override int CountAtInitialization =>
        elements!.Count;

    private protected override int CurrentCount =>
        ReadOnContext(SynchronizationContext, () => elements!.Count);

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
        ReadOnContext(SynchronizationContext, () => (IEnumerator<TElement>)elements!.ToList().GetEnumerator());

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
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
        var resetElements = e.Action is NotifyCollectionChangedAction.Reset ? source.ToList() : null;
        SynchronizationContext.Post(_ =>
        {
            using var changeHold = HoldOwnChanges();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    ListEdits.InsertAnnounced(elements!, e.NewStartingIndex, e.NewItems!);
                    break;
                case NotifyCollectionChangedAction.Move:
                    elements!.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    elements!.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    ListEdits.ReplaceAnnounced(elements!, e.OldStartingIndex, e.OldItems!, e.NewItems!);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    elements!.Reset(resetElements!);
                    break;
            }
        }, null);
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            SynchronizationContext.Post(_ =>
            {
                using var changeHold = HoldOwnChanges();
                OperationFault = source.OperationFault;
            }, null);
    }

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext} eventually";
}
