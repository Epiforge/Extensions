namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableQuery :
    SyncDisposable,
    IObservableQuery
{
    protected static readonly PropertyChangedEventArgs countPropertyChangedEventArgs = new(nameof(IReadOnlyList<object>.Count));
    protected static readonly PropertyChangingEventArgs countPropertyChangingEventArgs = new(nameof(IReadOnlyList<object>.Count));

    public ObservableQuery(CollectionObserver collectionObserver) =>
        this.collectionObserver = collectionObserver;

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
        }
    }

    protected abstract void OnInitialization();
}