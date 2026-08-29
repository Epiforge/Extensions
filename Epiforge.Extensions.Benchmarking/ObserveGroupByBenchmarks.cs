namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObserveGroupByBenchmarks :
    CollectionQueryBenchmarks
{
    IObservableCollectionQuery<IObservableGrouping<int, BenchmarkPerson>> groupBy = null!;

    protected override void CleanupQuery() =>
        groupBy.Dispose();

    [Benchmark(Baseline = true)]
    public int Count() =>
        groupBy.Count;

    [Benchmark]
    public int Enumerate()
    {
        var count = 0;
        foreach (var grouping in groupBy)
            ++count;
        return count;
    }

    [Benchmark]
    public void KeyChange() =>
        Next().Rank = NextKey();

    protected override void SetupQuery() =>
        groupBy = SourceQuery.ObserveGroupBy(person => person.Rank % 16);

    [Benchmark]
    public void SourceAddAndRemove()
    {
        Source.Add(Added);
        Source.RemoveAt(Source.Count - 1);
    }
}
