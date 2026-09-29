namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Measures the projection a grouped query is built on, pairing each element with its key, at the sizes and under the workload of <c>GroupedScaleComparisonBenchmarks</c>, so that a difference between versions in the grouped figures can be placed in the projection or above it
/// </summary>
/// <remarks>
/// A grouped query pairs each element with its key through a projection of its own and moves elements between groups on that projection's announcements. This arm builds the same projection over the same collection, with a <see cref="Tuple{T1, T2}" /> as the pair so that the lambda is the same in both versions, and subscribes to it as a grouped query does, so that it announces what it would announce to one. Its floor is <c>GroupedScaleComparisonBenchmarks.ChangeAThousandRanksUnobserved</c>, which makes the same changes to the same collection
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class PairingProjectionScaleBenchmarks
{
    const int changeCount = 1000;
    const int groupCount = 16;

    static readonly NotifyCollectionChangedEventHandler ignore = (sender, e) => { };

    IObservableCollectionQuery<Tuple<BenchmarkPerson, int>> pairs = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    int stride;

    [Params(1_000, 4_000, 10_000)]
    public int ElementCount { get; set; }

    [Benchmark]
    public void ChangeAThousandRanksWithExpressions()
    {
        for (var i = 0; i < changeCount; ++i)
            source[i * stride].Rank ^= 2;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        pairs.CollectionChanged -= ignore;
        pairs.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        source = BenchmarkPerson.CreateCollection(ElementCount);
        stride = ElementCount / changeCount;
        sourceQuery = new CollectionObserver().ObserveReadOnlyList(source);
        pairs = sourceQuery.ObserveSelect(person => new Tuple<BenchmarkPerson, int>(person, person.Rank % groupCount));
        pairs.CollectionChanged += ignore;
        var subject = source[0];
        var held = subject.Rank;
        subject.Rank = held + 1;
        if (pairs[0].Item1 != subject || pairs[0].Item2 != (held + 1) % groupCount)
            throw new InvalidOperationException("this library's projection did not pair an element with the key it changed to");
        subject.Rank = held;
        if (pairs[0].Item2 != held % groupCount)
            throw new InvalidOperationException("this library's projection did not pair an element with the key it changed back to");
    }
}
