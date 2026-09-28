namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableQuery :
    SyncDisposable,
    IObservableQuery
{
    /// <summary>
    /// Holds the locks under which one or more queries change, taken in the order the queries were constructed, until disposed of
    /// </summary>
    private protected readonly ref struct ChangeHold
    {
        internal ChangeHold(ObservableQuery? source)
        {
            if (source is null)
                return;
            if (!source.ChangesThroughOthers)
            {
                source.EnterChangeLock();
                single = source;
                return;
            }
            var queries = RentChangeLocks();
            source.CollectChangeLocks(queries);
            many = Order(queries);
        }

        internal ChangeHold(IEnumerable<ObservableQuery> sources)
        {
            var queries = RentChangeLocks();
            foreach (var source in sources)
                source.CollectChangeLocks(queries);
            many = Order(queries);
        }

        internal ChangeHold(ObservableQuery first, ObservableQuery? second)
        {
            if (!first.ChangesThroughOthers && (second is null || !second.ChangesThroughOthers))
            {
                if (second is not null && second.Sequence < first.Sequence)
                    (first, second) = (second, first);
                first.EnterChangeLock();
                single = first;
                if (second is not null && !ReferenceEquals(second, first))
                {
                    second.EnterChangeLock();
                    this.second = second;
                }
                return;
            }
            var queries = RentChangeLocks();
            first.CollectChangeLocks(queries);
            second?.CollectChangeLocks(queries);
            many = Order(queries);
        }

        readonly List<ObservableQuery>? many;
        readonly ObservableQuery? second;
        readonly ObservableQuery? single;

        public void Dispose()
        {
            second?.ExitChangeLock();
            single?.ExitChangeLock();
            if (many is not null)
            {
                for (var i = many.Count - 1; i >= 0; --i)
                    many[i].ExitChangeLock();
                many.Clear();
                spareChangeLocks = many;
            }
        }

        /// <summary>
        /// Keeps the list a finished hold on this thread ordered its locks in for the next such hold, so that holding several locks allocates nothing once a thread has done it
        /// </summary>
        [ThreadStatic]
        static List<ObservableQuery>? spareChangeLocks;

        static List<ObservableQuery> RentChangeLocks()
        {
            if (spareChangeLocks is { } queries)
            {
                spareChangeLocks = null;
                return queries;
            }
            return [];
        }

        static List<ObservableQuery> Order(List<ObservableQuery> queries)
        {
            queries.Sort(static (x, y) => x.Sequence.CompareTo(y.Sequence));
            var distinct = 0;
            for (var i = 0; i < queries.Count; ++i)
                if (distinct == 0 || !ReferenceEquals(queries[distinct - 1], queries[i]))
                    queries[distinct++] = queries[i];
            queries.RemoveRange(distinct, queries.Count - distinct);
            for (var i = 0; i < queries.Count; ++i)
                queries[i].EnterChangeLock();
            return queries;
        }
    }

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
        internal int Depth;
        internal object? First;
        internal List<object>? Many;
        internal object? Second;
        internal object? Third;
    }

    /// <summary>
    /// Holds the lock under which a query changes, without holding back the notifications raised while it is held, until disposed of
    /// </summary>
    private protected readonly ref struct OwnChangeHold
    {
        internal OwnChangeHold(ObservableQuery query)
        {
            var owner = query.ChangeLockOwner;
            if (owner.IsChangeLockHeldByCurrentThread)
                return;
            owner.EnterChangeLock();
            this.owner = owner;
        }

        readonly ObservableQuery? owner;

        public void Dispose() =>
            owner?.ExitChangeLock();
    }

    protected static readonly PropertyChangedEventArgs countPropertyChangedEventArgs = new(nameof(IReadOnlyList<>.Count));
    protected static readonly PropertyChangingEventArgs countPropertyChangingEventArgs = new(nameof(IReadOnlyList<>.Count));
    static long lastSequence;

    public ObservableQuery(CollectionObserver collectionObserver)
    {
        this.collectionObserver = collectionObserver;
        Logger = collectionObserver.TraceLogger;
    }

#if IS_NET_9_0_OR_GREATER
    Lock? changeAccess;
#else
    object? changeAccess;
#endif
#if IS_NET_9_0_OR_GREATER
    Lock? childrenAccess;
#else
    object? childrenAccess;
#endif
    protected readonly CollectionObserver collectionObserver;
    NotificationDeferralState? deferralState;
    int dependentSequence;
    ObservableQuerySubscription? firstDependent;
#if IS_NET_9_0_OR_GREATER
    readonly Lock lifetimeAccess = new();
#else
    readonly object lifetimeAccess = new();
#endif
    bool isInitialized;
    ObservableQuerySubscription? lastDependent;

    internal int Observations;

    /// <summary>
    /// Orders queries by construction, so that a query is later than every query it is built over and locks taken in this order are taken in the order changes flow
    /// </summary>
    internal readonly long Sequence = Interlocked.Increment(ref lastSequence);

    public virtual int CachedObservableQueries { get; } = 0;

    /// <summary>
    /// Gets the query whose lock this query changes under, which is another query only where that query alone ever changes this one
    /// </summary>
    private protected virtual ObservableQuery? ChangeLockHolder =>
        null;

    /// <summary>
    /// Gets whether reading this query reads other queries as they stand rather than only what this query has kept, so that holding this query still requires holding those
    /// </summary>
    internal virtual bool ChangesThroughOthers =>
        false;

    /// <summary>
    /// Gets the lock guarding what this query keeps of the queries built over it, which is its caches of them and its list of the queries derived from it, created by whichever first needs it, so that a query nothing is built over allocates none
    /// </summary>
#if IS_NET_9_0_OR_GREATER
    private protected Lock ChildrenAccess
#else
    private protected object ChildrenAccess
#endif
    {
        get
        {
            if (Volatile.Read(ref childrenAccess) is { } access)
                return access;
            access = new();
            return Interlocked.CompareExchange(ref childrenAccess, access, null) ?? access;
        }
    }

    public ICollectionObserver CollectionObserver =>
        collectionObserver;

#if IS_NET_9_0_OR_GREATER
    private protected Lock? ExistingChildrenAccess =>
#else
    private protected object? ExistingChildrenAccess =>
#endif
        Volatile.Read(ref childrenAccess);

    private protected bool HasDependents =>
        Volatile.Read(ref firstDependent) is not null;

    /// <summary>
    /// Gets the lock under which this query changes and announces its changes, created by whichever first needs it, so that a query which never changes and is never built over allocates none
    /// </summary>
#if IS_NET_9_0_OR_GREATER
    Lock ChangeAccess
#else
    object ChangeAccess
#endif
    {
        get
        {
            if (Volatile.Read(ref changeAccess) is { } access)
                return access;
            access = new();
            return Interlocked.CompareExchange(ref changeAccess, access, null) ?? access;
        }
    }

    /// <summary>
    /// Gets the query whose lock this query changes under, following each query which delegates its lock to another
    /// </summary>
    ObservableQuery ChangeLockOwner
    {
        get
        {
            var query = this;
            while (query.ChangeLockHolder is { } holder && !ReferenceEquals(holder, query))
                query = holder;
            return query;
        }
    }

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

    /// <summary>
    /// Gets whether the current thread holds this query's own lock, in which case holding it again would change nothing
    /// </summary>
    bool IsChangeLockHeldByCurrentThread =>
#if IS_NET_9_0_OR_GREATER
        Volatile.Read(ref changeAccess) is { } access && access.IsHeldByCurrentThread;
#else
        Volatile.Read(ref changeAccess) is { } access && Monitor.IsEntered(access);
#endif

    void BeginNotificationDeferral()
    {
#if IS_NET_9_0_OR_GREATER
        ChangeAccess.Enter();
#else
        Monitor.Enter(ChangeAccess);
#endif
        ++DeferralState.Depth;
    }

    /// <summary>
    /// Adds this query and every query reading this one reads through to the specified list
    /// </summary>
    internal virtual void CollectChangeLocks(List<ObservableQuery> queries) =>
        queries.Add(this);

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
            changeAccess!.Exit();
#else
            Monitor.Exit(changeAccess!);
#endif
        }
    }

    internal void EnterChangeLock()
    {
        if (ChangeLockHolder is { } holder && !ReferenceEquals(holder, this))
        {
            holder.EnterChangeLock();
            return;
        }
#if IS_NET_9_0_OR_GREATER
        ChangeAccess.Enter();
#else
        Monitor.Enter(ChangeAccess);
#endif
    }

    internal void ExitChangeLock()
    {
        if (ChangeLockHolder is { } holder && !ReferenceEquals(holder, this))
        {
            holder.ExitChangeLock();
            return;
        }
#if IS_NET_9_0_OR_GREATER
        changeAccess!.Exit();
#else
        Monitor.Exit(changeAccess!);
#endif
    }

    /// <summary>
    /// Holds the specified query, and whatever it reads through, unchanged until the returned scope ends, so that what this query reads of it and its subscription to it describe the same moment
    /// </summary>
    private protected static ChangeHold HoldChangesOf(ObservableQuery? source) =>
        new(source);

    /// <summary>
    /// Holds the specified queries, and whatever they read through, unchanged until the returned scope ends
    /// </summary>
    private protected static ChangeHold HoldChangesOf(ObservableQuery first, ObservableQuery? second) =>
        new(first, second);

    /// <summary>
    /// Holds the specified queries, and whatever they read through, unchanged until the returned scope ends
    /// </summary>
    private protected static ChangeHold HoldChangesOf(IEnumerable<ObservableQuery> sources) =>
        new(sources);

    /// <summary>
    /// Holds this query's own changes back for the lifetime of the returned scope, so that a change made and announced within it cannot interleave with a query being built over this one
    /// </summary>
    private protected OwnChangeHold HoldOwnChanges() =>
        new(this);

    internal void Initialize()
    {
        lock (lifetimeAccess)
        {
            if (isInitialized)
                return;
            OnInitialization();
            isInitialized = true;
            collectionObserver.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryInitialized, "Initialized observation of {Query}", this);
        }
    }

    /// <summary>
    /// Delivers a notification to the queries derived from this one, in the order they subscribed, skipping any which subscribed after delivery began, since such a query was built from the state this notification describes and would otherwise take it twice
    /// </summary>
    private protected void NotifyDependents(object eventArguments)
    {
        var bound = Volatile.Read(ref dependentSequence);
        var current = Volatile.Read(ref firstDependent);
        while (current is not null && unchecked(current.Sequence - bound) <= 0)
        {
            var following = current.Next;
            if (!current.IsRemoved)
            {
                if (eventArguments is NotifyCollectionChangedEventArgs collectionChangedEventArgs)
                    current.Dependent.OnDependencyCollectionChanged(current, collectionChangedEventArgs);
                else if (eventArguments is PropertyChangedEventArgs propertyChangedEventArgs)
                    current.Dependent.OnDependencyPropertyChanged(current, propertyChangedEventArgs);
                else if (eventArguments is PropertyChangingEventArgs propertyChangingEventArgs)
                    current.Dependent.OnDependencyPropertyChanging(current, propertyChangingEventArgs);
            }
            current = following;
        }
    }

    protected abstract void OnInitialization();

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!DeferNotification(e))
            RaisePropertyChanged(e);
    }

    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (!DeferNotification(e))
            RaisePropertyChanging(e);
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
            RaisePropertyChanged(propertyChangedEventArgs);
        else if (eventArguments is PropertyChangingEventArgs propertyChangingEventArgs)
            RaisePropertyChanging(propertyChangingEventArgs);
    }

    void RaisePropertyChanged(PropertyChangedEventArgs e)
    {
        NotifyDependents(e);
        base.OnPropertyChanged(e);
    }

    void RaisePropertyChanging(PropertyChangingEventArgs e)
    {
        NotifyDependents(e);
        base.OnPropertyChanging(e);
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
        collectionObserver.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_QueryDisposed, "Disposed observation of {Query}", this);

    /// <summary>
    /// Subscribes a query derived from this one to its notifications, which it will receive before any handler subscribed to this query's events
    /// </summary>
    internal ObservableQuerySubscription SubscribeDependent(IObservableQueryDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        lock (ChildrenAccess)
        {
            var sequence = unchecked(dependentSequence + 1);
            var subscription = new ObservableQuerySubscription(dependent, sequence);
            subscription.Previous = lastDependent;
            if (lastDependent is null)
                Volatile.Write(ref firstDependent, subscription);
            else
                lastDependent.Next = subscription;
            lastDependent = subscription;
            Volatile.Write(ref dependentSequence, sequence);
            return subscription;
        }
    }

    /// <summary>
    /// Gets the query a scoped collection query stands for
    /// </summary>
    private protected static ObservableQuery Unscoped<T>(IObservableCollectionQuery<T> query) =>
        query is ScopedObservableCollectionQuery<T> scoped ? scoped.query : (ObservableQuery)query;

    /// <summary>
    /// Gets the query a scoped dictionary query stands for
    /// </summary>
    private protected static ObservableQuery Unscoped<TKey, TValue>(IObservableDictionaryQuery<TKey, TValue> query)
        where TKey : notnull =>
        query is ScopedObservableDictionaryQuery<TKey, TValue> scoped ? scoped.query : (ObservableQuery)query;

    internal void UnsubscribeDependent(ObservableQuerySubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (ChildrenAccess)
        {
            if (subscription.IsRemoved)
                return;
            subscription.IsRemoved = true;
            if (subscription.Previous is null)
                Volatile.Write(ref firstDependent, subscription.Next);
            else
                subscription.Previous.Next = subscription.Next;
            if (subscription.Next is null)
                lastDependent = subscription.Previous;
            else
                subscription.Next.Previous = subscription.Previous;
            subscription.Previous = null;
        }
    }
}
