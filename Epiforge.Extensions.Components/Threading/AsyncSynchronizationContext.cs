namespace Epiforge.Extensions.Components.Threading;

/// <summary>
/// Provides a <see cref="SynchronizationContext"/> that uses the Task Parallel Library (TPL) to process callbacks asynchronously
/// </summary>
public sealed class AsyncSynchronizationContext :
    SynchronizationContext,
    IDisposable
{
    class QueuedCallback
    {
        public SendOrPostCallback Callback { get; }
        public object? State { get; }
        public ManualResetEventSlim? Signal { get; }
        public Exception? Exception { get; set; }

        public QueuedCallback(SendOrPostCallback callback, object? state, ManualResetEventSlim? signal)
        {
            Callback = callback;
            State = state;
            Signal = signal;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncSynchronizationContext"/> class
    /// </summary>
    public AsyncSynchronizationContext()
    {
        queuedCallbacks = new();
        Task.Run(ProcessCallbacksAsync);
    }

    /// <summary>
    /// Finalizes this object
    /// </summary>
    ~AsyncSynchronizationContext() =>
        Dispose(false);

    bool isDisposed;
    readonly AsyncProducerConsumerQueue<QueuedCallback> queuedCallbacks;

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() =>
        new AsyncSynchronizationContext();

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (disposing)
            queuedCallbacks.CompleteAdding();
        isDisposed = true;
    }

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(d);
        queuedCallbacks.Enqueue(new QueuedCallback(d, state, null));
    }

    async Task ProcessCallbacksAsync()
    {
        while (await queuedCallbacks.OutputAvailableAsync().ConfigureAwait(false))
        {
            var queuedCallback = await queuedCallbacks.DequeueAsync().ConfigureAwait(false);
            var currentContext = Current;
            SetSynchronizationContext(this);
            try
            {
                queuedCallback.Callback(queuedCallback.State);
            }
            catch (Exception ex)
            {
                queuedCallback.Exception = ex;
            }
            try
            {
                queuedCallback.Signal?.Set();
            }
            finally
            {
                SetSynchronizationContext(currentContext);
            }
        }
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(d);
        using var signal = new ManualResetEventSlim(false);
        var queuedCallback = new QueuedCallback(d, state, signal);
        queuedCallbacks.Enqueue(queuedCallback);
        signal.Wait();
        if (queuedCallback.Exception is { } exception)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

#if IS_NET_7_0_OR_GREATER
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#endif
    void ThrowIfDisposed()
    {
#if IS_NET_7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(isDisposed, this);
#else
        if (isDisposed)
            throw new ObjectDisposedException(GetType().Name);
#endif
    }
}
