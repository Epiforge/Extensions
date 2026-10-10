namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices the range operations of a range collection announcing once per operation on the front half of a collection, where an operation which shifts the rest of the collection once for each element it touches costs the most
/// </summary>
/// <remarks>
/// Each iteration fills a fresh collection, so each operation is measured once on a collection of the stated size. The released job runs the Collections released with Expressions 7.1.2
/// </remarks>
[AgainstReleasedExpressions("7.1.2")]
[MemoryDiagnoser]
[IterationCount(40)]
[ProcessCount(3)]
public class RangeCollectionSpanBenchmarks
{
    int[] half = null!;
    int halfCount;
    int[] quarter = null!;
    ObservableRangeCollection<int> range = null!;
    int[] source = null!;

    [Params(1000, 16000)]
    public int ElementCount { get; set; }

    [IterationSetup]
    public void FillCollection() =>
        range = new ObservableRangeCollection<int>(source);

    [Benchmark]
    public void InsertRangeOfHalfAtStart() =>
        range.InsertRange(0, half);

    [Benchmark]
    public void MoveRangeOfFrontHalfToEnd() =>
        range.MoveRange(0, halfCount, halfCount);

    [Benchmark]
    public void RemoveAllOfFrontHalf() =>
        range.RemoveAll(value => value < halfCount);

    [Benchmark]
    public void RemoveRangeOfFrontHalf() =>
        range.RemoveRange(0, halfCount);

    [Benchmark]
    public void ReplaceRangeOfFrontHalfWithQuarter() =>
        range.ReplaceRange(0, halfCount, quarter);

    [GlobalSetup]
    public void Setup()
    {
        source = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            source[i] = i;
        halfCount = ElementCount / 2;
        half = new int[halfCount];
        for (var i = 0; i < halfCount; ++i)
            half[i] = ElementCount + i;
        quarter = new int[ElementCount / 4];
        for (var i = 0; i < quarter.Length; ++i)
            quarter[i] = ElementCount + i;
    }
}
