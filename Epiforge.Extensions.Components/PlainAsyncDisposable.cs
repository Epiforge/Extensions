namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides an overridable mechanism for releasing unmanaged resources asynchronously, without property change notification and without a finalizer
/// </summary>
/// <remarks>
/// Because this class declares no finalizer, disposal will not occur if consumers neglect it; hold unmanaged resources in a <see cref="SafeHandle"/>
/// </remarks>
public abstract class PlainAsyncDisposable :
    IAsyncDisposable,
    INotifyDisposalOverridden,
    IDisposalStatus,
    INotifyDisposed,
    INotifyDisposing
{
    int disposalClaim;

    /// <summary>
    /// Gets whether this object has been disposed
    /// </summary>
    public bool IsDisposed { get; private set; }

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
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref disposalClaim, 1, 0) != 0)
            return;
        var e = EventArgs.Empty;
        var disposed = false;
        try
        {
            Disposing?.Invoke(this, e);
            disposed = await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            if (!disposed)
                Interlocked.Exchange(ref disposalClaim, 0);
        }
        if (disposed)
        {
            GC.SuppressFinalize(this);
            IsDisposed = true;
            Disposed?.Invoke(this, e);
        }
        else
            DisposalOverridden?.Invoke(this, e);
    }

    /// <summary>
    /// Frees, releases, or resets resources
    /// </summary>
    /// <returns>true if this object was disposed; false to override disposal</returns>
    protected abstract ValueTask<bool> DisposeAsyncCore();

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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
#else
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
#endif
    }
}
