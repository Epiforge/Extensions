namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationContextQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, SynchronizationContext synchronizationContext) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    Action? applyPendingChangesAction;
    ObservableRangeCollection<TElement>? elements;
    Action? sendPendingChangesAction;
    readonly ConcurrentQueue<(NotifyCollectionChangedEventArgs change, List<TElement>? reset)> pending = new();
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

    public override TElement this[int index] =>
        ReadOnContext(SynchronizationContext, () => elements![index]);

    public override int Count =>
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
    /// Applies, on the context's thread, every change of the source not yet applied, in the order the source announced them
    /// </summary>
    void ApplyPendingChanges()
    {
        using var changeHold = HoldOwnChanges();
        while (pending.TryDequeue(out var next))
            Apply(next.change, next.reset);
    }

    /// <summary>
    /// Yields the one delegate this query hands on for applying its pending changes, since a method group converts to a new delegate at each conversion and this one is handed on for every change of the source
    /// </summary>
    Action ApplyPendingChangesAction =>
        applyPendingChangesAction ??= ApplyPendingChanges;

    /// <summary>
    /// Yields the one delegate this query defers for sending its pending changes to the context to be applied, since a lambda capturing the query converts to a new delegate each time it is reached, which is once for every change of the source
    /// </summary>
    Action SendPendingChangesAction =>
        sendPendingChangesAction ??= () => SynchronizationContext.Send(ApplyPendingChangesAction);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var reset = e.Action is NotifyCollectionChangedAction.Reset ? [.. source] : (List<TElement>?)null;
        if (SynchronizationContext == SynchronizationContext.Current && pending.IsEmpty)
        {
            using var changeHold = HoldOwnChanges();
            Apply(e, reset);
            return;
        }
        pending.Enqueue((e, reset));
        if (SynchronizationContext == SynchronizationContext.Current)
            ApplyPendingChanges();
        else
            DeferUntilChangeLocksReleased(SendPendingChangesAction);
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OperationFault))
            return;
        void applyOperationFault()
        {
            using var changeHold = HoldOwnChanges();
            OperationFault = source.OperationFault;
        }
        if (SynchronizationContext == SynchronizationContext.Current)
            applyOperationFault();
        else
            DeferUntilChangeLocksReleased(() => SynchronizationContext.Send(applyOperationFault));
    }

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext}";
}
