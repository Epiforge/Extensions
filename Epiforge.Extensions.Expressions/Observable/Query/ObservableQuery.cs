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

    /// <summary>
    /// Holds what a query needs only while it defers notifications, created by the first mutation which defers them, so that a query which never changes allocates none of it
    /// </summary>
    sealed class NotificationDeferralState
    {
#if IS_NET_9_0_OR_GREATER
        internal readonly Lock Access = new();
#else
        internal readonly object Access = new();
#endif
        internal int Depth;
        internal object? First;
        internal List<object>? Many;
        internal object? Second;
        internal object? Third;
    }

    protected static readonly PropertyChangedEventArgs countPropertyChangedEventArgs = new(nameof(IReadOnlyList<>.Count));
    protected static readonly PropertyChangingEventArgs countPropertyChangingEventArgs = new(nameof(IReadOnlyList<>.Count));

    public ObservableQuery(CollectionObserver collectionObserver)
    {
        this.collectionObserver = collectionObserver;
        Logger = collectionObserver.ExpressionObserver.Logger;
    }

    protected readonly CollectionObserver collectionObserver;
    NotificationDeferralState? deferralState;
#if IS_NET_9_0_OR_GREATER
    readonly Lock lifetimeAccess = new();
#else
    readonly object lifetimeAccess = new();
#endif
    bool isInitialized;

    internal int Observations;

    public virtual int CachedObservableQueries { get; } = 0;

    public ICollectionObserver CollectionObserver =>
        collectionObserver;

    NotificationDeferralState DeferralState
    {
        get
        {
            if (Volatile.Read(ref deferralState) is { } state)
                return state;
            state = new();
            return Interlocked.CompareExchange(ref deferralState, state, null) ?? state;
        }
    }

    void BeginNotificationDeferral()
    {
        var state = DeferralState;
#if IS_NET_9_0_OR_GREATER
        state.Access.Enter();
#else
        Monitor.Enter(state.Access);
#endif
        ++state.Depth;
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
        if (Volatile.Read(ref deferralState) is not { Depth: > 0 } state)
            return false;
        if (state.First is null)
            state.First = eventArguments;
        else if (state.Second is null)
            state.Second = eventArguments;
        else if (state.Third is null)
            state.Third = eventArguments;
        else
            (state.Many ??= []).Add(eventArguments);
        return true;
    }

    void EndNotificationDeferral()
    {
        var state = deferralState!;
        object? first = null, second = null, third = null;
        List<object>? many = null;
        if (--state.Depth == 0)
        {
            first = state.First;
            second = state.Second;
            third = state.Third;
            many = state.Many;
            state.First = null;
            state.Second = null;
            state.Third = null;
            state.Many = null;
        }
        try
        {
            RaiseDeferredNotifications(first, second, third, many);
        }
        finally
        {
#if IS_NET_9_0_OR_GREATER
            state.Access.Exit();
#else
            Monitor.Exit(state.Access);
#endif
        }
    }

    internal void Initialize()
    {
        lock (lifetimeAccess)
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

    /// <summary>
    /// Releases one observation of this query, one release at a time and never during initialization, since disposal ignores a call made while another is under way and a release ignored is an observation never released
    /// </summary>
    internal void Release()
    {
        lock (lifetimeAccess)
            Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        collectionObserver.ExpressionObserver.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryDisposed, "Disposed observation of {Query}", this);
}
