namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkPersonWithCachedEventArguments :
    PropertyChangeNotifier
{
    static readonly PropertyChangedEventArgs rankChanged = new(nameof(Rank));
    static readonly PropertyChangingEventArgs rankChanging = new(nameof(Rank));

    public BenchmarkPersonWithCachedEventArguments(int rank) =>
        this.rank = rank;

    int rank;

    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value, rankChanging, rankChanged);
    }
}
