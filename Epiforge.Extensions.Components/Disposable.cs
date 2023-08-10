namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides an overridable mechanism for releasing unmanaged resources asynchronously or synchronously
/// </summary>
public abstract class Disposable :
    PropertyChangeNotifier,
    IAsyncDisposable,
    IDisposable,
    INotifyDisposalOverridden,
    IDisposalStatus,
    INotifyDisposed,
    INotifyDisposing
{
    /// <summary>
    /// Finalizes this object
    /// </summary>
    [ExcludeFromCodeCoverage]
    ~Disposable()
    {
        Logger?.LogTrace("Finalizer called");
        var e = DisposalNotificationEventArgs.ByFinalizer;
        OnDisposing(e);
        Dispose(false);
        IsDisposed = true;
        OnDisposed(e);
    }

    readonly AsyncLock disposalAccess = new();
    bool isDisposed;

    /// <summary>
    /// Gets whether this object has been disposed
    /// </summary>
	public bool IsDisposed
    {
        get => isDisposed;
        private set => SetBackedProperty(ref isDisposed, in value, IsDisposedPropertyChanging, IsDisposedPropertyChanged);
    }

    /// <summary>
    /// Occurs when this object's disposal has been overridden
    /// </summary>
    public event EventHandler<DisposalNotificationEventArgs>? DisposalOverridden;

    /// <summary>
    /// Occurs when this object has been disposed
    /// </summary>
    public event EventHandler<DisposalNotificationEventArgs>? Disposed;

    /// <summary>
    /// Occurs when this object is being disposed
    /// </summary>
    public event EventHandler<DisposalNotificationEventArgs>? Disposing;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
    /// </summary>
    public virtual void Dispose()
    {
        Logger?.LogTrace("Dispose called");
        using (disposalAccess.Lock())
            if (!IsDisposed)
            {
                var e = DisposalNotificationEventArgs.ByCallingDispose;
                OnDisposing(e);
                if (IsDisposed = Dispose(true))
                {
                    OnDisposed(e);
                    GC.SuppressFinalize(this);
                }
                else
                    OnDisposalOverridden(e);
            }
    }

    /// <summary>
    /// Frees, releases, or resets unmanaged resources
    /// </summary>
    /// <param name="disposing">false if invoked by the finalizer because the object is being garbage collected; otherwise, true</param>
    /// <returns>true if disposal completed; otherwise, false</returns>
    protected abstract bool Dispose(bool disposing);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        Logger?.LogTrace("DisposeAsync called");
        using (await disposalAccess.LockAsync().ConfigureAwait(false))
            if (!IsDisposed)
            {
                var e = DisposalNotificationEventArgs.ByCallingDispose;
                OnDisposing(e);
                if (IsDisposed = await DisposeAsync(true).ConfigureAwait(false))
                {
                    OnDisposed(e);
                    GC.SuppressFinalize(this);
                }
                else
                    OnDisposalOverridden(e);
            }
    }

    /// <summary>
    /// Frees, releases, or resets unmanaged resources
    /// </summary>
    /// <param name="disposing">false if invoked by the finalizer because the object is being garbage collected; otherwise, true</param>
    /// <returns>true if disposal completed; otherwise, false</returns>
    protected abstract ValueTask<bool> DisposeAsync(bool disposing);

    void OnDisposalOverridden(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace("Raising DisposalOverridden event");
        DisposalOverridden?.Invoke(this, e);
        Logger?.LogTrace("Raised DisposalOverridden event");
    }

    void OnDisposed(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace("Raising Disposed event");
        Disposed?.Invoke(this, e);
        Logger?.LogTrace("Raised Disposed event");
    }

    void OnDisposing(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace("Raising Disposing event");
        Disposing?.Invoke(this, e);
        Logger?.LogTrace("Raised Disposing event");
    }

    /// <summary>
    /// Ensure the object has not been disposed
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object has already been disposed</exception>
    protected void ThrowIfDisposed()
    {
        if (isDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    internal static readonly PropertyChangedEventArgs IsDisposedPropertyChanged = new(nameof(IsDisposed));
    internal static readonly PropertyChangingEventArgs IsDisposedPropertyChanging = new(nameof(IsDisposed));
}
