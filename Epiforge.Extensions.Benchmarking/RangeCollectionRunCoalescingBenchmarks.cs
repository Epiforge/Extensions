namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class RangeCollectionRunCoalescingBenchmarks
{
    ObservableRangeCollection<int> batched = null!;
    Func<int, bool> doomed = null!;
    ObservableRangeCollection<int> individual = null!;
    int[] source = null!;

    [Params(1000, 16000)]
    public int ElementCount { get; set; }

    [IterationSetup]
    public void FillCollections()
    {
        batched = new ObservableRangeCollection<int>(source, false);
        individual = new ObservableRangeCollection<int>(source, true);
    }

    [Params("Clustered", "Scattered")]
    public string Matches { get; set; } = null!;

    [Benchmark(Baseline = true)]
    public void RemoveAllRaisingEachElement() =>
        individual.RemoveAll(doomed);

    [Benchmark]
    public void RemoveAllRaisingEachRun() =>
        batched.RemoveAll(doomed);

    [GlobalSetup]
    public void Setup()
    {
        source = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            source[i] = i;
        var quarter = ElementCount / 4;
        doomed = Matches switch
        {
            "Clustered" => value => value < quarter,
            _ => value => value % 4 == 0
        };
    }
}
