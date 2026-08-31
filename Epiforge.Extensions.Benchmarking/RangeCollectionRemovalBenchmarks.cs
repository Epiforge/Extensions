namespace Epiforge.Extensions.Benchmarking;

using System.Collections.ObjectModel;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class RangeCollectionRemovalBenchmarks
{
    int[] doomed = null!;
    ObservableCollection<int> plain = null!;
    ObservableRangeCollection<int> range = null!;
    int[] source = null!;

    [Params(1000, 4000, 16000)]
    public int ElementCount { get; set; }

    [IterationSetup]
    public void FillCollections()
    {
        plain = new ObservableCollection<int>(source);
        range = new ObservableRangeCollection<int>(source);
    }

    [Benchmark(Baseline = true)]
    public void RemoveEachScatteredItem()
    {
        for (var i = 0; i < doomed.Length; ++i)
            plain.Remove(doomed[i]);
    }

    [Benchmark]
    public void RemoveRangeOfScatteredItems() =>
        range.RemoveRange(doomed);

    [GlobalSetup]
    public void Setup()
    {
        source = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            source[i] = i;
        doomed = new int[100];
        var stride = ElementCount / doomed.Length;
        for (var i = 0; i < doomed.Length; ++i)
            doomed[i] = i * stride;
    }
}
