namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableQuery :
    SyncDisposable,
    IObservableQuery
{
    /// <summary>
    /// Holds a query's notifications back until the mutation which produced them has released the lock guarding its state, so that no subscriber ever runs while that lock is held
    /// </summary>
    private protected readonly ref struct NotificationDeferral
    {
        internal NotificationDeferral(ObservableQuery query)
        {
            this.query = query;
            query.BeginNotificationDeferral();
        }

        readonly ObservableQuery query;

        public void Dispose() =>
            query.EndNotificationDeferral();
    }

    protected static readonly PropertyChangedEventArgs countPropertyChangedEventArgs = new(nameof(IReadOnlyList<>.Count));
    protected static readonly PropertyChangingEventArgs countPropertyChangingEventArgs = new(nameof(IReadOnlyList<>.Count));

    public ObservableQuery(CollectionObserver collectionObserver)
    {
        this.collectionObserver = collectionObserver;
        Logger = collectionObserver.ExpressionObserver.Logger;
    }

    protected readonly CollectionObserver collectionObserver;
    object? deferredNotifications;
#if IS_NET_9_0_OR_GREATER
    readonly Lock initializationAccess = new();
#else
    readonly object initializationAccess = new();
#endif
    bool isInitialized;
    int notificationDeferralDepth;
#if IS_NET_9_0_OR_GREATER
    readonly Lock notificationAccess = new();
#else
    readonly object notificationAccess = new();
#endif

    internal int Observations;

    public virtual int CachedObservableQueries { get; } = 0;

    public ICollectionObserver CollectionObserver =>
        collectionObserver;

    void BeginNotificationDeferral()
    {
#if IS_NET_9_0_OR_GREATER
        notificationAccess.Enter();
#else
        Monitor.Enter(notificationAccess);
#endif
        ++notificationDeferralDepth;
    }

    /// <summary>
    /// Holds this query's notifications back for the lifetime of the returned scope, raising them in the order they were produced once the outermost scope ends
    /// </summary>
    private protected NotificationDeferral DeferNotificationsUntilMutationCompletes() =>
        new(this);

    /// <summary>
    /// Records notification arguments to be raised when the outermost deferral scope ends, returning <c>false</c> when no deferral is in progress and the caller should raise them itself
    /// </summary>
    private protected bool DeferNotification(object eventArguments)
    {
        if (notificationDeferralDepth == 0)
            return false;
        if (deferredNotifications is null)
            deferredNotifications = eventArguments;
        else if (deferredNotifications is List<object> manyDeferredNotifications)
            manyDeferredNotifications.Add(eventArguments);
        else
            deferredNotifications = new List<object> { deferredNotifications, eventArguments };
        return true;
    }

    void EndNotificationDeferral()
    {
        object? deferred = null;
        if (--notificationDeferralDepth == 0)
        {
            deferred = deferredNotifications;
            deferredNotifications = null;
        }
        try
        {
            RaiseDeferredNotifications(deferred);
        }
        finally
        {
#if IS_NET_9_0_OR_GREATER
            notificationAccess.Exit();
#else
            Monitor.Exit(notificationAccess);
#endif
        }
    }

    internal void Initialize()
    {
        lock (initializationAccess)
        {
            if (isInitialized)
                return;
            OnInitialization();
            isInitialized = true;
            collectionObserver.ExpressionObserver.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryInitialized, "Initialized observation of {Query}", this);
        }
    }

    protected abstract void OnInitialization();

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!DeferNotification(e))
            base.OnPropertyChanged(e);
    }

    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (!DeferNotification(e))
            base.OnPropertyChanging(e);
    }

    void RaiseDeferredNotifications(object? deferred)
    {
        if (deferred is List<object> manyDeferredNotifications)
            for (int i = 0, ii = manyDeferredNotifications.Count; i < ii; ++i)
                RaiseNotification(manyDeferredNotifications[i]);
        else if (deferred is not null)
            RaiseNotification(deferred);
    }

    /// <summary>
    /// Raises the event which the specified deferred notification arguments describe
    /// </summary>
    private protected virtual void RaiseNotification(object eventArguments)
    {
        if (eventArguments is PropertyChangedEventArgs propertyChangedEventArgs)
            base.OnPropertyChanged(propertyChangedEventArgs);
        else if (eventArguments is PropertyChangingEventArgs propertyChangingEventArgs)
            base.OnPropertyChanging(propertyChangingEventArgs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        collectionObserver.ExpressionObserver.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryDisposed, "Disposed observation of {Query}", this);
}
