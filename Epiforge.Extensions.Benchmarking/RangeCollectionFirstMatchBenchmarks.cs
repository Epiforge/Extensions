namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class RangeCollectionFirstMatchBenchmarks
{
    Func<int, bool> doomed = null!;
    ObservableRangeCollection<int> range = null!;
    int[] source = null!;

    [Params(1000, 16000)]
    public int ElementCount { get; set; }

    [IterationSetup]
    public void FillCollection() =>
        range = new ObservableRangeCollection<int>(source);

    [Params("First", "Last", "None")]
    public string Match { get; set; } = null!;

    [Benchmark]
    public void ResetRemovingAllMatches() =>
        range.ResetRemovingAll(doomed);

    [GlobalSetup]
    public void Setup()
    {
        source = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            source[i] = i;
        var last = ElementCount - 1;
        doomed = Match switch
        {
            "First" => value => value == 0,
            "Last" => value => value == last,
            _ => value => false
        };
    }
}
