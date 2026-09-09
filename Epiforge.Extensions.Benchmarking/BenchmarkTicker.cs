namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Stands in for an entity whose setter raises several notifications for one logical change, and which passes pre-allocated arguments so that a raise allocates nothing
/// </summary>
/// <remarks>
/// An application moving a running interval forward once a second changes one thing and announces three, against every observation attached to that object whatever property each of them watches. What that costs is not visible in an instrument whose subject announces one property at a time, which is why this one announces three and carries a fourth nothing watches
/// </remarks>
public sealed class BenchmarkTicker :
    PropertyChangeNotifier
{
    static readonly PropertyChangedEventArgs durationChanged = new(nameof(Duration));
    static readonly PropertyChangingEventArgs durationChanging = new(nameof(Duration));
    static readonly PropertyChangedEventArgs endChanged = new(nameof(End));
    static readonly PropertyChangingEventArgs endChanging = new(nameof(End));
    static readonly PropertyChangedEventArgs idleChanged = new(nameof(Idle));
    static readonly PropertyChangingEventArgs idleChanging = new(nameof(Idle));
    static readonly PropertyChangedEventArgs startChanged = new(nameof(Start));
    static readonly PropertyChangingEventArgs startChanging = new(nameof(Start));

    long duration;
    long end;
    long idle;
    long start;

    public long Duration
    {
        get => duration;
        set => SetBackedProperty(ref duration, in value, durationChanging, durationChanged);
    }

    /// <summary>
    /// The property every observation in these arms watches
    /// </summary>
    public long End
    {
        get => end;
        set => SetBackedProperty(ref end, in value, endChanging, endChanged);
    }

    /// <summary>
    /// A property no observation in these arms watches, which is how a raise nobody wants is priced
    /// </summary>
    public long Idle
    {
        get => idle;
        set => SetBackedProperty(ref idle, in value, idleChanging, idleChanged);
    }

    public long Start
    {
        get => start;
        set => SetBackedProperty(ref start, in value, startChanging, startChanged);
    }

    /// <summary>
    /// Moves the interval forward, announcing the three properties one such move changes
    /// </summary>
    public void Advance(long ticks)
    {
        var movedEnd = end + ticks;
        SetBackedProperty(ref end, in movedEnd, endChanging, endChanged);
        var movedDuration = movedEnd - start;
        SetBackedProperty(ref duration, in movedDuration, durationChanging, durationChanged);
        var movedStart = start + 0;
        SetBackedProperty(ref start, in movedStart, startChanging, startChanged);
    }
}
