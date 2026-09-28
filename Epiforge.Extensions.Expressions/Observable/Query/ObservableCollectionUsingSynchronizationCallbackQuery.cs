namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationCallbackQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, object context, CollectionSynchronizationCallback synchronizationCallback) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    ObservableRangeCollection<TElement>? elements;
    readonly Queue<(NotifyCollectionChangedEventArgs change, List<TElement>? reset)> pending = new();
    internal readonly object Context = context;
    internal readonly CollectionSynchronizationCallback SynchronizationCallback = synchronizationCallback;

    public override TElement this[int index] =>
        elements![index];

    public override int Count =>
        elements!.Count;

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
        elements!.GetEnumerator();

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

    /// <summary>
    /// Applies, within the callback, every change of the source not yet applied, in the order the source announced them
    /// </summary>
    void ApplyPendingChanges()
    {
        using var changeHold = HoldOwnChanges();
        while (true)
        {
            NotifyCollectionChangedEventArgs e;
            List<TElement>? reset;
            lock (pending)
            {
                if (!pending.TryDequeue(out var next))
                    return;
                (e, reset) = next;
            }
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
                    elements!.Reset(reset!);
                    break;
            }
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (pending)
            pending.Enqueue((e, e.Action is NotifyCollectionChangedAction.Reset ? [.. source] : null));
        DeferUntilChangeLocksReleased(() => SynchronizationCallback(this, Context, ApplyPendingChanges, true));
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            DeferUntilChangeLocksReleased(() => SynchronizationCallback(this, Context, () =>
            {
                using var changeHold = HoldOwnChanges();
                OperationFault = source.OperationFault;
            }, false));
    }

    public override string ToString() =>
        $"synchronizing {source} using callback";
}
