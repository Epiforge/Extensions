namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveAverageBenchmarks :
    CollectionQueryBenchmarks
{
    IObservableScalarQuery<double> average = null!;

    protected override void CleanupQuery() =>
        average.Dispose();

    protected override void SetupQuery() =>
        average = SourceQuery.ObserveAverage(person => (double)person.Rank);

    [Benchmark]
    public void ValueChange() =>
        Next().Rank = NextKey();
}
