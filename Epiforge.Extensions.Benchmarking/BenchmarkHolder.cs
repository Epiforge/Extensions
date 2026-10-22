namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkHolder :
    PropertyChangeNotifier
{
    public BenchmarkHolder(BenchmarkHolding holding) =>
        this.holding = holding;

    BenchmarkHolding holding;
    int score;

    public BenchmarkHolding Holding
    {
        get => holding;
        set => SetBackedProperty(ref holding, in value);
    }

    public int Score
    {
        get => score;
        set => SetBackedProperty(ref score, in value);
    }
}
