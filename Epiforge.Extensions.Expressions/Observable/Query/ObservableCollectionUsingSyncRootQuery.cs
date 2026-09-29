namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSyncRootQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, object syncRoot) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    Action? applyPendingChangesAction;
    ObservableRangeCollection<TElement>? elements;
    readonly ConcurrentQueue<(NotifyCollectionChangedEventArgs change, List<TElement>? reset)> pending = new();

    public override TElement this[int index] =>
        elements![index];

    public override int Count =>
        elements!.Count;

    public override object SyncRoot { get; } = syncRoot;

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
    /// Applies one change of the source to what this query keeps
    /// </summary>
    void Apply(NotifyCollectionChangedEventArgs e, List<TElement>? reset)
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
                elements!.Reset(reset!);
                break;
        }
    }

    /// <summary>
    /// Applies, while holding the lock, every change of the source not yet applied, in the order the source announced them
    /// </summary>
    void ApplyPendingChanges()
    {
        lock (SyncRoot!)
        {
            using var changeHold = HoldOwnChanges();
            while (pending.TryDequeue(out var next))
                Apply(next.change, next.reset);
        }
    }

    /// <summary>
    /// Yields the one delegate this query hands on for applying its pending changes, since a method group converts to a new delegate at each conversion and this one is handed on for every change of the source
    /// </summary>
    Action ApplyPendingChangesAction =>
        applyPendingChangesAction ??= ApplyPendingChanges;

    /// <remarks>
    /// Where the only change lock this thread holds is the source's, and the lock can be taken without waiting, the change is applied at once, after any still pending, since nothing waited and nothing the thread is handling is left to finish first; otherwise it waits in the queue until this thread holds no change lock
    /// </remarks>
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var reset = e.Action is NotifyCollectionChangedAction.Reset ? [.. source] : (List<TElement>?)null;
        if (HoldsOneChangeLock && Monitor.TryEnter(SyncRoot!))
        {
            try
            {
                using var changeHold = HoldOwnChanges();
                while (pending.TryDequeue(out var next))
                    Apply(next.change, next.reset);
                Apply(e, reset);
            }
            finally
            {
                Monitor.Exit(SyncRoot!);
            }
            return;
        }
        pending.Enqueue((e, reset));
        DeferUntilChangeLocksReleased(ApplyPendingChangesAction);
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            DeferUntilChangeLocksReleased(() =>
            {
                lock (SyncRoot!)
                {
                    using var changeHold = HoldOwnChanges();
                    OperationFault = source.OperationFault;
                }
            });
    }

    public override string ToString() =>
        $"synchronizing {source} using {SyncRoot}";
}
