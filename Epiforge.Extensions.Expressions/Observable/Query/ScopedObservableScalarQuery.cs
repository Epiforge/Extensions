namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ScopedObservableScalarQuery<TResult> :
    IObservableScalarQuery<TResult>
{
    internal ScopedObservableScalarQuery(ObservableScalarQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        this.query = query;
    }

#if IS_NET_9_0_OR_GREATER
    Lock? changeAccess;
#else
    object? changeAccess;
#endif
    PropertyChangedEventHandler? propertyChanged;
    PropertyChangingEventHandler? propertyChanging;
    readonly ObservableScalarQuery<TResult> query;
    int disposed;

    /// <summary>
    /// Gets the lock guarding this wrapper's subscriptions to its query, created by whichever subscription first needs it, so that a wrapper nothing subscribes to allocates none
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

    public int CachedObservableQueries =>
        query.CachedObservableQueries;

    public ICollectionObserver CollectionObserver =>
        query.CollectionObserver;

    public (Exception? Fault, TResult Result) Evaluation =>
        query.Evaluation;

    public bool IsDisposed =>
        disposed != 0;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            lock (ChangeAccess)
            {
                if (IsDisposed)
                    return;
                if (propertyChanged is null)
                    query.PropertyChanged += QueryPropertyChanged;
                propertyChanged += value;
            }
        }
        remove
        {
            lock (ChangeAccess)
            {
                if (propertyChanged is null)
                    return;
                propertyChanged -= value;
                if (propertyChanged is null)
                    query.PropertyChanged -= QueryPropertyChanged;
            }
        }
    }

    public event PropertyChangingEventHandler? PropertyChanging
    {
        add
        {
            lock (ChangeAccess)
            {
                if (IsDisposed)
                    return;
                if (propertyChanging is null)
                    query.PropertyChanging += QueryPropertyChanging;
                propertyChanging += value;
            }
        }
        remove
        {
            lock (ChangeAccess)
            {
                if (propertyChanging is null)
                    return;
                propertyChanging -= value;
                if (propertyChanging is null)
                    query.PropertyChanging -= QueryPropertyChanging;
            }
        }
    }

    public event EventHandler? Disposed;

    public event EventHandler? Disposing;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = EventArgs.Empty;
        Disposing?.Invoke(this, e);
        if (Volatile.Read(ref changeAccess) is { } access)
            lock (access)
                DetachWithAccess();
        query.Dispose();
        Disposed?.Invoke(this, e);
    }

    /// <summary>
    /// Detaches from the query every handler this wrapper attached on behalf of its own subscribers, and forgets those subscribers
    /// </summary>
    void DetachWithAccess()
    {
        if (propertyChanged is not null)
        {
            query.PropertyChanged -= QueryPropertyChanged;
            propertyChanged = null;
        }
        if (propertyChanging is not null)
        {
            query.PropertyChanging -= QueryPropertyChanging;
            propertyChanging = null;
        }
    }

    public IObservableScalarQuery<TTransform> ObserveTransform<TTransform>(Expression<Func<TResult, TTransform>> transform) =>
        query.ObserveTransform(transform);

    void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        propertyChanged?.Invoke(this, e);

    void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        propertyChanging?.Invoke(this, e);

    public override string ToString() =>
        query.ToString() ?? string.Empty;
}
