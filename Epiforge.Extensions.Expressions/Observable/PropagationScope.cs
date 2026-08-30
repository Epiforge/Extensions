namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Marks the thread as propagating a change through the graph for the lifetime of the scope, holding each affected observation's notification back until the outermost scope ends so that no consumer sees an evaluation which was never simultaneously true of the graph's inputs
/// </summary>
readonly ref struct PropagationScope
{
    [ThreadStatic]
    static int depth;
    [ThreadStatic]
    static List<ScopedObservableExpression>? pending;

    /// <summary>
    /// Gets whether the calling thread is within a propagation
    /// </summary>
    internal static bool IsPropagating =>
        depth != 0;

    /// <summary>
    /// Records an observation to be notified when the outermost scope ends, which each observation may do only once per propagation
    /// </summary>
    internal static void Enlist(ScopedObservableExpression scopedObservableExpression) =>
        (pending ??= []).Add(scopedObservableExpression);

    public PropagationScope() =>
        ++depth;

    [SuppressMessage("Code Analysis", "CA1508: Avoid dead conditional code", Justification = "raising a notification can reenter this scope and assign the field, which the analyzer cannot see")]
    [SuppressMessage("Code Analysis", "CA1822: Mark members as static", Justification = "the using statement binds to an instance method, so a scope whose whole state is thread-static still cannot be static")]
    public void Dispose()
    {
        if (--depth != 0 || pending is not { Count: > 0 } flushing)
            return;
        pending = null;
        for (int i = 0, ii = flushing.Count; i < ii; ++i)
            flushing[i].ClearPendingNotification();
        for (int i = 0, ii = flushing.Count; i < ii; ++i)
            flushing[i].RaisePendingNotification();
        flushing.Clear();
        pending ??= flushing;
    }
}
