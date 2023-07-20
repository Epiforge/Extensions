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
        queuedCallbacksCancellationTokenSource = new();
        Task.Run(ProcessCallbacksAsync);
    }

    /// <summary>
    /// Finalizes this object
    /// </summary>
    ~AsyncSynchronizationContext() =>
        Dispose(false);

    readonly AsyncProducerConsumerQueue<QueuedCallback> queuedCallbacks;
    readonly CancellationTokenSource queuedCallbacksCancellationTokenSource;

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() =>
        this;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (disposing)
        {
            queuedCallbacksCancellationTokenSource.Cancel();
            queuedCallbacksCancellationTokenSource.Dispose();
        }
    }

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(d);
#else
        if (d is null)
            throw new ArgumentNullException(nameof(d));
#endif
        queuedCallbacks.Enqueue(new QueuedCallback(d, state, null));
    }

    async Task ProcessCallbacksAsync()
    {
        while (true)
        {
            var queuedCallback = await queuedCallbacks.DequeueAsync(queuedCallbacksCancellationTokenSource.Token).ConfigureAwait(false);
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
            queuedCallback.Signal?.Set();
            SetSynchronizationContext(currentContext);
        }
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(d);
#else
        if (d is null)
            throw new ArgumentNullException(nameof(d));
#endif
        using var signal = new ManualResetEventSlim(false);
        var queuedCallback = new QueuedCallback(d, state, signal);
        queuedCallbacks.Enqueue(queuedCallback);
        signal.Wait();
        if (queuedCallback.Exception is { } exception)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
