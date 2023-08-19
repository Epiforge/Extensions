namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationCallbackEventuallyQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionUsingSynchronizationCallbackEventuallyQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, object context, CollectionSynchronizationCallback synchronizationCallback) :
        base(collectionObserver)
    {
        disposedCancellationTokenSource = new();
        pendingCollectionChangedEventsQueue = new();
        resetCancellationTokenSources = new();
        this.source = source;
        Context = context;
        SynchronizationCallback = synchronizationCallback;
    }

    AsyncProducerConsumerQueue<NotifyCollectionChangedEventArgs>? currentPendingCollectionChangedEvents;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    CancellationTokenSource? currentResetCancellationTokenSource;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    readonly CancellationTokenSource disposedCancellationTokenSource;
    RangeObservableCollection<TElement>? elements;
    readonly ConcurrentQueue<AsyncProducerConsumerQueue<NotifyCollectionChangedEventArgs>> pendingCollectionChangedEventsQueue;
    readonly ConcurrentQueue<CancellationTokenSource> resetCancellationTokenSources;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly object Context;
    internal readonly CollectionSynchronizationCallback SynchronizationCallback;

    public override TElement this[int index] =>
        elements![index];

    public override int Count =>
        elements!.Count;

    public override Exception? OperationFault
    {
        get => source.OperationFault;
        protected set => throw new NotImplementedException();
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                disposedCancellationTokenSource.Cancel();
                disposedCancellationTokenSource.Dispose();
                while (resetCancellationTokenSources.TryDequeue(out var resetCancellationTokenSource))
                    resetCancellationTokenSource.Dispose();
                source.CollectionChanged -= SourceCollectionChanged;
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
        currentPendingCollectionChangedEvents = new();
        pendingCollectionChangedEventsQueue.Enqueue(currentPendingCollectionChangedEvents);
        currentResetCancellationTokenSource = new();
        resetCancellationTokenSources.Enqueue(currentResetCancellationTokenSource);
        elements = new(source);
        source.CollectionChanged += SourceCollectionChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
        Task.Run(SynchronizationAsync);
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset)
        {
            currentPendingCollectionChangedEvents = new();
            pendingCollectionChangedEventsQueue.Enqueue(currentPendingCollectionChangedEvents);
            var previousResetCancellationTokenSource = currentResetCancellationTokenSource;
            currentResetCancellationTokenSource = new();
            resetCancellationTokenSources.Enqueue(currentResetCancellationTokenSource);
            previousResetCancellationTokenSource!.Cancel();
        }
        else
            currentPendingCollectionChangedEvents!.Enqueue(e);
    }

    async Task SynchronizationAsync()
    {
        var disposedCancellationToken = disposedCancellationTokenSource.Token;
        while (pendingCollectionChangedEventsQueue.TryDequeue(out var pendingCollectionChangeEvents) && resetCancellationTokenSources.TryDequeue(out var resetCancellationTokenSource))
            using (resetCancellationTokenSource)
            {
                try
                {
                    using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(resetCancellationTokenSource.Token, disposedCancellationToken);
                    while (true)
                    {
                        var e = await pendingCollectionChangeEvents.DequeueAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
                        SynchronizationCallback(this, Context, () =>
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
                            }
                        }, true);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (disposedCancellationToken.IsCancellationRequested)
                        return;
                    SynchronizationCallback(this, Context, () => elements!.Reset(source), true);
                }
            }
    }

    public override string ToString() =>
        $"synchronizing {source} using callback eventually";
}
