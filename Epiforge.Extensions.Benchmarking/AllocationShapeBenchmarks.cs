namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices the forms a one-element sequence can take, because a collection expression targeting an interface does not allocate what the array it appears to be would, and a term of this library's allocation model was predicted eight bytes high for assuming it did
/// </summary>
[MemoryDiagnoser]
public class AllocationShapeBenchmarks
{
    static readonly object element = new();

    [Benchmark]
    public object[] ArrayOfOne() =>
        new object[1];

    [Benchmark]
    public object[] ArrayOfTwo() =>
        new object[2];

    [Benchmark]
    public object?[] CollectionExpressionToArray() =>
        [element];

    [Benchmark(Baseline = true)]
    public IReadOnlyList<object?> CollectionExpressionToReadOnlyList() =>
        [element];

    [Benchmark]
    public IReadOnlyList<object?> CollectionExpressionOfTwoToReadOnlyList() =>
        [element, element];

    [Benchmark]
    public IReadOnlyList<object?> EmptyCollectionExpressionToReadOnlyList() =>
        [];
}
