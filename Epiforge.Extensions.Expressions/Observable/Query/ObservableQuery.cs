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
    object? firstDeferredNotification;
    List<object>? manyDeferredNotifications;
    object? secondDeferredNotification;
    object? thirdDeferredNotification;
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
        if (firstDeferredNotification is null)
            firstDeferredNotification = eventArguments;
        else if (secondDeferredNotification is null)
            secondDeferredNotification = eventArguments;
        else if (thirdDeferredNotification is null)
            thirdDeferredNotification = eventArguments;
        else
            (manyDeferredNotifications ??= []).Add(eventArguments);
        return true;
    }

    void EndNotificationDeferral()
    {
        object? first = null, second = null, third = null;
        List<object>? many = null;
        if (--notificationDeferralDepth == 0)
        {
            first = firstDeferredNotification;
            second = secondDeferredNotification;
            third = thirdDeferredNotification;
            many = manyDeferredNotifications;
            firstDeferredNotification = null;
            secondDeferredNotification = null;
            thirdDeferredNotification = null;
            manyDeferredNotifications = null;
        }
        try
        {
            RaiseDeferredNotifications(first, second, third, many);
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

    void RaiseDeferredNotifications(object? first, object? second, object? third, List<object>? many)
    {
        if (first is null)
            return;
        RaiseNotification(first);
        if (second is null)
            return;
        RaiseNotification(second);
        if (third is null)
            return;
        RaiseNotification(third);
        if (many is not null)
            for (int i = 0, ii = many.Count; i < ii; ++i)
                RaiseNotification(many[i]);
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
