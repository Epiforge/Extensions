namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkHolding :
    PropertyChangeNotifier
{
    public BenchmarkHolding(int rank) =>
        this.rank = rank;

    int rank;

    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value);
    }
}
