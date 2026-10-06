namespace Epiforge.Extensions.Blazor;

/// <summary>
/// The reactive components of one dispatcher which have been told of a change and have not yet rendered for it, so that they are rendered together in one batch rather than one batch apiece
/// </summary>
sealed class NotificationQueue
{
    static readonly SendOrPostCallback flushCallback = state => ((NotificationQueue)state!).Flush();
    static readonly ConditionalWeakTable<Dispatcher, NotificationQueue> queues = new();

    public static NotificationQueue For(Dispatcher dispatcher) =>
        queues.GetValue(dispatcher, static dispatcher => new NotificationQueue(dispatcher));

    NotificationQueue(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        flush = Flush;
        flushAfterYielding = FlushAfterYieldingAsync;
    }

    readonly Dispatcher dispatcher;
    readonly Action flush;
    readonly Func<Task> flushAfterYielding;
    readonly ConcurrentQueue<ReactiveComponentBase> pending = new();
    int scheduled;

    public bool IsEmpty =>
        pending.IsEmpty;

    /// <summary>
    /// Renders every pending component, which, called while a batch is being rendered, puts them all in that batch
    /// </summary>
    public void Drain()
    {
        while (pending.TryDequeue(out var component))
            component.FlushNotifications();
    }

    public void Enqueue(ReactiveComponentBase component)
    {
        pending.Enqueue(component);
        if (Interlocked.Exchange(ref scheduled, 1) != 0)
            return;
        if (dispatcher.CheckAccess() && SynchronizationContext.Current is { } synchronizationContext)
            synchronizationContext.Post(flushCallback, this);
        else
            _ = dispatcher.InvokeAsync(flushAfterYielding);
    }

    void Flush()
    {
        Volatile.Write(ref scheduled, 0);
        Drain();
    }

    /// <summary>
    /// Flushes after the work in progress, since a dispatcher which is idle runs what it is handed at once on the thread which handed it over, and that thread is usually still announcing changes
    /// </summary>
    async Task FlushAfterYieldingAsync()
    {
        await Task.Yield();
        if (dispatcher.CheckAccess())
            Flush();
        else
            await dispatcher.InvokeAsync(flush);
    }
}
