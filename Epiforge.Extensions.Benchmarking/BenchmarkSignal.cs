namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkSignal :
    PropertyChangeNotifier
{
    static readonly PropertyChangedEventArgs rankChangedEventArgs = new(nameof(Rank));

    object marker = new();
    int rank;

    public object Marker
    {
        get => marker;
        set => SetBackedProperty(ref marker, in value);
    }

    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value);
    }

    public void TouchRank() =>
        OnPropertyChanged(rankChangedEventArgs);
}
