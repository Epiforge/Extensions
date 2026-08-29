namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class FastComparisonBenchmarks
{
    object boxedFirst = null!;
    object boxedSecond = null!;
    FastComparer comparer = null!;
    FastEqualityComparer equalityComparer = null!;
    int first;
    int second;

    [Benchmark]
    public int ComparerCompare() =>
        comparer.Compare(boxedFirst, boxedSecond);

    [Benchmark]
    public int DefaultCompare() =>
        Comparer<int>.Default.Compare(first, second);

    [Benchmark(Baseline = true)]
    public bool DefaultEquals() =>
        EqualityComparer<int>.Default.Equals(first, second);

    [Benchmark]
    public bool EqualityComparerEquals() =>
        equalityComparer.Equals(boxedFirst, boxedSecond);

    [Benchmark]
    public int EqualityComparerGetHashCode() =>
        equalityComparer.GetHashCode(boxedFirst);

    [GlobalSetup]
    public void Setup()
    {
        first = 4;
        second = 5;
        boxedFirst = first;
        boxedSecond = second;
        comparer = FastComparer.Get(typeof(int));
        equalityComparer = FastEqualityComparer.Get(typeof(int));
    }
}
