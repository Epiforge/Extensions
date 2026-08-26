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
        if (loggerSetStackTrace is null)
            Logger?.LogWarning(EventIds.Epiforge_Extensions_Components_FinalizerCalled, "Finalizer called: did you forget to dispose an object? (set logging minimum level to Trace to see the stack trace for when the Logger was set)");
        else
            Logger?.LogWarning(EventIds.Epiforge_Extensions_Components_FinalizerCalled, "Finalizer called: did you forget to dispose an object? (stack trace for when the Logger was set: {LoggerSetStackTrace})", loggerSetStackTrace);
        Dispose(false);
        isDisposed = true;
    }

    int disposalClaim;
    bool isDisposed;
    string? loggerSetStackTrace;

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
    public event EventHandler? DisposalOverridden;

    /// <summary>
    /// Occurs when this object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    /// <summary>
    /// Occurs when this object is being disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
    /// </summary>
    public void Dispose()
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_DisposeCalled, "Dispose called");
        if (Interlocked.CompareExchange(ref disposalClaim, 1, 0) != 0)
            return;
        var e = EventArgs.Empty;
        var disposed = false;
        try
        {
            OnDisposing(e);
            disposed = IsDisposed = Dispose(true);
        }
        finally
        {
            if (!disposed)
                Interlocked.Exchange(ref disposalClaim, 0);
        }
        if (disposed)
        {
            OnDisposed(e);
            GC.SuppressFinalize(this);
        }
        else
            OnDisposalOverridden(e);
    }

    /// <summary>
    /// Frees, releases, or resets unmanaged resources
    /// </summary>
    /// <param name="disposing">false if invoked by the finalizer because the object is being garbage collected; otherwise, true</param>
    /// <returns>true if this object was disposed; false to override disposal</returns>
    protected abstract bool Dispose(bool disposing);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_DisposeCalled, "DisposeAsync called");
        if (Interlocked.CompareExchange(ref disposalClaim, 1, 0) != 0)
            return;
        var e = EventArgs.Empty;
        var disposed = false;
        try
        {
            OnDisposing(e);
            disposed = IsDisposed = await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            if (!disposed)
                Interlocked.Exchange(ref disposalClaim, 0);
        }
        if (disposed)
        {
            OnDisposed(e);
            GC.SuppressFinalize(this);
        }
        else
            OnDisposalOverridden(e);
    }

    /// <summary>
    /// Frees, releases, or resets resources
    /// </summary>
    /// <returns>true if this object was disposed; false to override disposal</returns>
    protected abstract ValueTask<bool> DisposeAsyncCore();

    /// <inheritdoc/>
    protected override void LoggerSet()
    {
        if (Logger?.IsEnabled(LogLevel.Trace) ?? false)
            loggerSetStackTrace = Environment.StackTrace;
    }

    void OnDisposalOverridden(EventArgs e)
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisingDisposalOverridden, "Raising DisposalOverridden event");
        DisposalOverridden?.Invoke(this, e);
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisedDisposalOverridden, "Raised DisposalOverridden event");
    }

    void OnDisposed(EventArgs e)
    {
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisingDisposed, "Raising Disposed event");
        Disposed?.Invoke(this, e);
        Logger?.LogTrace(EventIds.Epiforge_Extensions_Components_RaisedDisposed, "Raised Disposed event");
    }

    void OnDisposing(EventArgs e)
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

    internal static readonly PropertyChangedEventArgs IsDisposedPropertyChanged = new(nameof(IsDisposed));
    internal static readonly PropertyChangingEventArgs IsDisposedPropertyChanging = new(nameof(IsDisposed));
}
