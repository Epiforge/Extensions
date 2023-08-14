namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableQuery :
    SyncDisposable,
    IObservableQuery
{
    protected static readonly PropertyChangedEventArgs countPropertyChangedEventArgs = new(nameof(IReadOnlyList<object>.Count));
    protected static readonly PropertyChangingEventArgs countPropertyChangingEventArgs = new(nameof(IReadOnlyList<object>.Count));

    public ObservableQuery(CollectionObserver collectionObserver)
    {
        this.collectionObserver = collectionObserver;
        Logger = collectionObserver.ExpressionObserver.Logger;
    }

    protected readonly CollectionObserver collectionObserver;
    readonly object initializationAccess = new();
    bool isInitialized;

    internal int Observations;

    public virtual int CachedObservableQueries { get; } = 0;

    public ICollectionObserver CollectionObserver =>
        collectionObserver;

    internal void Initialize()
    {
        lock (initializationAccess)
        {
            if (isInitialized)
                return;
            OnInitialization();
            isInitialized = true;
            if (collectionObserver.ExpressionObserver.Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace("Initialized observation of {Query}", this);
        }
    }

    protected abstract void OnInitialization();

    protected void RemovedFromCache()
    {
        if (collectionObserver.ExpressionObserver.Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Disposed observation of {Query}", this);
    }
}