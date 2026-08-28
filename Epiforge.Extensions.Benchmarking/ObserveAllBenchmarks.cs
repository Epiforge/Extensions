namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveAllBenchmarks :
    CollectionQueryBenchmarks
{
    IObservableScalarQuery<bool> all = null!;

    protected override void CleanupQuery() =>
        all.Dispose();

    protected override void SetupQuery() =>
        all = SourceQuery.ObserveAll(person => person.Rank >= 0);

    [Benchmark]
    public void ValueChangeTogglingTheResult()
    {
        var person = Next();
        person.Rank = -person.Rank;
    }
}
