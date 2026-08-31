namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class RangeCollectionPredicateRemovalBenchmarks
{
    Func<int, bool> doomed = null!;
    ObservableRangeCollection<int> range = null!;
    int[] source = null!;

    [Params(1000, 16000)]
    public int ElementCount { get; set; }

    [IterationSetup]
    public void FillCollection() =>
        range = new ObservableRangeCollection<int>(source);

    [Benchmark(Baseline = true)]
    public void RemoveAllMatches() =>
        range.RemoveAll(doomed);

    [Params(1, 25, 75)]
    public int RemovedPercent { get; set; }

    [Benchmark]
    public void ResetRemovingAllMatches() =>
        range.ResetRemovingAll(doomed);

    [Benchmark]
    public void ResetToSurvivors()
    {
        var survivors = new List<int>(range.Count);
        for (var i = 0; i < range.Count; ++i)
        {
            var item = range[i];
            if (!doomed(item))
                survivors.Add(item);
        }
        range.Reset(survivors);
    }

    [GlobalSetup]
    public void Setup()
    {
        source = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            source[i] = i;
        var removedPercent = RemovedPercent;
        doomed = value => value % 100 < removedPercent;
    }
}
