namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// An observation which announces a node's evaluation to its consumer, holding its announcement back until the propagation which changed the node has ended
/// </summary>
interface IEvaluationAnnouncer
{
    void ClearPendingNotification();

    void RaiseEvaluationChanged();

    void RaiseEvaluationChanging();

    void RaisePendingNotification();

    bool ResultEquals(object? x, object? y);
}

/// <summary>
/// Decides when an announcer announces, for the wrapper a graph observation is consumed through and for an observation made directly, which is consumed as itself, so that the two cannot drift apart in what a consumer sees
/// </summary>
static class EvaluationAnnouncement
{
    /// <summary>
    /// Stands in for an evaluation not yet read, so that whichever of construction and a first announcement reads the node's evaluation last is the one kept, since construction subscribes before it reads and an announcement can arrive in between on another thread
    /// </summary>
    internal static readonly object Unread = new();

    /// <summary>
    /// Announces an evaluation, a write which is a full fence where observations are shared between threads so that the node read after it is read no earlier than the write
    /// </summary>
    static void Announce(IEvaluationAnnouncer announcer, ref object? announcedState, ref bool notificationForced, object? state, bool isFenced)
    {
        notificationForced = false;
        announcer.RaiseEvaluationChanging();
        if (isFenced)
            Interlocked.Exchange(ref announcedState, state);
        else
            Volatile.Write(ref announcedState, state);
        announcer.RaiseEvaluationChanged();
    }

    /// <summary>
    /// Defers an announcer's notification to the end of the propagation on this thread, unless one is already pending
    /// </summary>
    /// <remarks>
    /// A pending notification may be another thread's, which may already have read the node without this thread's change, so where observations are shared between threads one seen pending is looked at again behind a full fence, as the thread raising it reads the node again behind one of its own before it finishes, and one of the two must then see the other
    /// </remarks>
    internal static bool Enlisted(IEvaluationAnnouncer announcer, ExpressionObserver observer, ref bool notificationPending)
    {
        if (!PropagationScope.IsPropagating)
            return false;
        if (Volatile.Read(ref notificationPending))
        {
            if (!observer.IsThreadSafe)
                return true;
            Interlocked.MemoryBarrier();
            if (Volatile.Read(ref notificationPending))
                return true;
        }
        Volatile.Write(ref notificationPending, true);
        PropagationScope.Enlist(announcer);
        return true;
    }

    /// <summary>
    /// Determines whether two faults are the same fault, which is by type and message rather than by identity because an expression which is re-evaluated while faulted throws a new exception every time, and announcing that as a change would make the number of notifications a consumer receives depend on how often the mechanism happens to re-evaluate
    /// </summary>
    static bool FaultEquals(Exception? x, Exception? y) =>
        ReferenceEquals(x, y) || x is not null && y is not null && x.GetType() == y.GetType() && x.Message == y.Message;

    static bool IsAnnounceable(IEvaluationAnnouncer announcer, ObservableExpression node, object? announcedState, object? currentState, bool notificationForced)
    {
        if (notificationForced)
            return true;
        var (announcedFault, announcedResult) = node.Decode(announcedState);
        var (currentFault, currentResult) = node.Decode(currentState);
        return !FaultEquals(announcedFault, currentFault) || !announcer.ResultEquals(announcedResult, currentResult);
    }

    /// <summary>
    /// Announces the node's evaluation if it differs from the one last announced
    /// </summary>
    /// <remarks>
    /// Where observations are shared between threads, two threads can announce at once having read the node at different moments, and the one writing last may write the older evaluation. So after announcing, or finding nothing to announce, a thread reads the node again behind a full fence and goes around again if it has moved, which leaves the last to write holding what the node holds
    /// </remarks>
    internal static void RaiseIfEvaluationChanged(IEvaluationAnnouncer announcer, ExpressionObserver observer, ObservableExpression node, ref object? announcedState, ref bool notificationForced)
    {
        var currentState = node.CurrentState;
        if (!observer.IsThreadSafe)
        {
            var announced = announcedState;
            if (ReferenceEquals(announced, Unread))
                announcedState = currentState;
            else if (IsAnnounceable(announcer, node, announced, currentState, notificationForced))
                Announce(announcer, ref announcedState, ref notificationForced, currentState, false);
            return;
        }
        while (true)
        {
            var announced = Volatile.Read(ref announcedState);
            if (ReferenceEquals(announced, Unread))
            {
                if (!ReferenceEquals(Interlocked.CompareExchange(ref announcedState, currentState, Unread), Unread))
                    continue;
            }
            else if (IsAnnounceable(announcer, node, announced, currentState, notificationForced))
                Announce(announcer, ref announcedState, ref notificationForced, currentState, true);
            else
                Interlocked.MemoryBarrier();
            var latestState = node.CurrentState;
            if (ReferenceEquals(latestState, currentState))
                return;
            currentState = latestState;
        }
    }

    /// <summary>
    /// Reads the node's evaluation as the first one announced, unless an announcement arriving since the announcer began listening has already read it
    /// </summary>
    internal static void TakeFirstEvaluation(ExpressionObserver observer, ObservableExpression node, ref object? announcedState)
    {
        if (!observer.IsThreadSafe)
            announcedState = node.CurrentState;
        else
            Interlocked.CompareExchange(ref announcedState, node.CurrentState, Unread);
    }
}

/// <summary>
/// Compares two results of one type as an announcer does to decide whether its evaluation changed
/// </summary>
static class AnnouncedResults<TResult>
{
    /// <summary>
    /// Whether results of this type are best compared as they arrive, already boxed, which is so for a value type that does not implement <see cref="IEquatable{T}"/> because its default comparer boxes one of them again to reach the very same comparison
    /// </summary>
    static readonly bool compareBoxed = typeof(TResult).IsValueType && !typeof(IEquatable<TResult>).IsAssignableFrom(typeof(TResult));

    internal static bool AreEqual(object? x, object? y) =>
        x is null || y is null ? ReferenceEquals(x, y) : compareBoxed ? x.Equals(y) : EqualityComparer<TResult>.Default.Equals((TResult)x, (TResult)y);
}
