namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides an overridable mechanism for releasing unmanaged resources asynchronously
/// </summary>
public abstract class AsyncDisposable :
    PropertyChangeNotifier,
    IAsyncDisposable,
    INotifyDisposalOverridden,
    IDisposalStatus,
    INotifyDisposed,
    INotifyDisposing
{
    /// <summary>
    /// Finalizes this object
    /// </summary>
    [ExcludeFromCodeCoverage]
    ~AsyncDisposable()
    {
        if (loggerSetStackTrace is null)
            Logger?.LogWarning(EventIds.Epiforge_Extensions_Components_FinalizerCalled, "Finalizer called: did you forget to dispose an object? (set logging minimum level to Trace to see the stack trace for when the Logger was set)");
        else
            Logger?.LogWarning(EventIds.Epiforge_Extensions_Components_FinalizerCalled, "Finalizer called: did you forget to dispose an object? (stack trace for when the Logger was set: {LoggerSetStackTrace})", loggerSetStackTrace);
        var e = DisposalNotificationEventArgs.ByCallingFinalizer;
        OnDisposing(e);
        DisposeAsync(false).AsTask().Wait();
        IsDisposed = true;
        OnDisposed(e);
    }

    readonly AsyncLock disposalAccess = new();
    bool isDisposed;
    string? loggerSetStackTrace;

    /// <summary>
    /// Gets whether this object has been disposed
    /// </summary>
    public bool IsDisposed
    {
        get => isDisposed;
        private set => SetBackedProperty(ref isDisposed, in value, Disposable.IsDisposedPropertyChanging, Disposable.IsDisposedPropertyChanged);
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
    public virtual async ValueTask DisposeAsync()
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_DisposeCalled, "DisposeAsync called");
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

    /// <inheritdoc/>
    protected override void LoggerSet()
    {
        if (Logger?.IsEnabled(LogLevel.Trace) ?? false)
            loggerSetStackTrace = Environment.StackTrace;
    }

    void OnDisposalOverridden(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisingDisposalOverridden, "Raising DisposalOverridden event");
        DisposalOverridden?.Invoke(this, e);
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisedDisposalOverridden, "Raised DisposalOverridden event");
    }

    void OnDisposed(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisingDisposed, "Raising Disposed event");
        Disposed?.Invoke(this, e);
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisedDisposed, "Raised Disposed event");
    }

    void OnDisposing(DisposalNotificationEventArgs e)
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisingDisposing, "Raising Disposing event");
        Disposing?.Invoke(this, e);
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisedDisposing, "Raised Disposing event");
    }

    /// <summary>
    /// Ensure the object has not been disposed
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object has already been disposed</exception>
#if IS_NET_7_0_OR_GREATER
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#endif
    protected void ThrowIfDisposed()
    {
#if IS_NET_7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(isDisposed, this);
#else
        if (isDisposed)
            throw new ObjectDisposedException(GetType().Name);
#endif
    }
}
