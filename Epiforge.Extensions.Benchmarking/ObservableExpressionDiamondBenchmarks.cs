namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class ObservableExpressionDiamondBenchmarks
{
    sealed class Probe
    {
        internal Probe(IObservableExpression<BenchmarkPerson, int> expression, BenchmarkPerson subject, int observerCost)
        {
            this.expression = expression;
            this.observerCost = observerCost;
            this.subject = subject;
            expression.PropertyChanged += ExpressionPropertyChanged;
        }

        readonly IObservableExpression<BenchmarkPerson, int> expression;
        readonly int observerCost;
        readonly BenchmarkPerson subject;

        internal int Notifications;

        internal void Dispose()
        {
            expression.PropertyChanged -= ExpressionPropertyChanged;
            expression.Dispose();
        }

        void ExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ++Notifications;
            Thread.SpinWait(observerCost);
        }

        internal void Toggle() =>
            subject.Rank ^= 1;
    }

    const int costlyObserver = 1000;
    const int freeObserver = 0;

    Probe chain = null!;
    Probe costlyChain = null!;
    Probe costlyDiamond = null!;
    Probe diamond = null!;
    ExpressionObserver observer = null!;

    [Benchmark(Baseline = true)]
    public void ChainChange() =>
        chain.Toggle();

    [GlobalCleanup]
    public void Cleanup()
    {
        chain.Dispose();
        costlyChain.Dispose();
        costlyDiamond.Dispose();
        diamond.Dispose();
    }

    [Benchmark]
    public void CostlyChainChange() =>
        costlyChain.Toggle();

    [Benchmark]
    public void CostlyDiamondChange() =>
        costlyDiamond.Toggle();

    [Benchmark]
    public void DiamondChange() =>
        diamond.Toggle();

    Probe NewChainProbe(int observerCost)
    {
        var subject = new BenchmarkPerson("chain", 4);
        return new(observer.Observe(person => person.Rank * 2 + 1, subject), subject, observerCost);
    }

    Probe NewDiamondProbe(int observerCost)
    {
        var subject = new BenchmarkPerson("diamond", 4);
        return new(observer.Observe(person => person.Rank * 2 + (person.Rank + 1), subject), subject, observerCost);
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new ExpressionObserver();
        chain = NewChainProbe(freeObserver);
        costlyChain = NewChainProbe(costlyObserver);
        costlyDiamond = NewDiamondProbe(costlyObserver);
        diamond = NewDiamondProbe(freeObserver);
    }
}
