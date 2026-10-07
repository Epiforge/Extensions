namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkDuration :
    PropertyChangeNotifier
{
    static readonly PropertyChangedEventArgs elapsedChanged = new(nameof(Elapsed));
    static readonly PropertyChangingEventArgs elapsedChanging = new(nameof(Elapsed));

    public BenchmarkDuration(TimeSpan elapsed) =>
        this.elapsed = elapsed;

    TimeSpan elapsed;

    public TimeSpan Elapsed
    {
        get => elapsed;
        set => SetBackedProperty(ref elapsed, in value, elapsedChanging, elapsedChanged);
    }
}
