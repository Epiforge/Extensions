namespace Epiforge.Extensions.Benchmarking;

using Epiforge.Extensions.Collections.Specialized;

[MemoryDiagnoser]
public class ReadOnlyWrapperBenchmarks
{
    const int copiedCount = 5000;
    const int elementCount = 10000;

    int[] destination = null!;
    LinkedList<int> mirror = null!;
    OrderedHashSet<int> set = null!;

    [Benchmark]
    public void CountedCopyToByWalk()
    {
        var copied = 0;
        for (var node = mirror.First; node is not null && copied < copiedCount; node = node.Next)
            destination[copied++] = node.Value;
    }

    [Benchmark(Baseline = true)]
    public void CountedCopyToThroughTheSet() =>
        set.CopyTo(destination, 0, copiedCount);

    [Benchmark]
    public void CountedCopyToWrapped() =>
        mirror.Cast<int>().Take(copiedCount).ToList().AsReadOnly().CopyTo(destination, 0);

    [GlobalSetup]
    public void Setup()
    {
        var elements = new int[elementCount];
        for (var i = 0; i < elementCount; ++i)
            elements[i] = i;
        mirror = new LinkedList<int>(elements);
        set = new OrderedHashSet<int>(elements);
        destination = new int[elementCount];
    }
}
