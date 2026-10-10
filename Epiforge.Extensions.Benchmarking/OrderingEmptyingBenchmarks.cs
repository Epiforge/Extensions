namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices an ordering going from empty to full and from full to empty, which it announced as resets through Expressions 7.1.2 and announces as an addition and a removal of every element since
/// </summary>
/// <remarks>
/// Each iteration builds a fresh ordering over a fresh source, so each change is measured once on an ordering of the stated size. Emptying removes every element from the source in one range, so in the released job it also pays the released Collections' removal from the source
/// </remarks>
[AgainstReleasedExpressions("7.1.2")]
[MemoryDiagnoser]
[IterationCount(40)]
[ProcessCount(3)]
public class OrderingEmptyingBenchmarks
{
    int[] descending = null!;
    CollectionObserver observer = null!;
    IObservableCollectionQuery<int> ordering = null!;
    ObservableRangeCollection<int> source = null!;
    IObservableCollectionQuery<int> sourceQuery = null!;

    [Params(1000, 16000)]
    public int ElementCount { get; set; }

    [IterationCleanup]
    public void DisposeOrdering()
    {
        ordering.Dispose();
        sourceQuery.Dispose();
    }

    [Benchmark]
    public void EmptyFullOrdering() =>
        source.RemoveRange(0, ElementCount);

    [Benchmark]
    public void FillEmptyOrdering() =>
        source.AddRange(descending);

    void ObserveOrdering()
    {
        sourceQuery = observer.ObserveReadOnlyList(source);
        ordering = sourceQuery.ObserveOrderBy(value => value);
    }

    [IterationSetup(Target = nameof(EmptyFullOrdering))]
    public void ObserveFullOrdering()
    {
        source = new ObservableRangeCollection<int>(descending);
        ObserveOrdering();
    }

    [IterationSetup(Target = nameof(FillEmptyOrdering))]
    public void ObserveEmptyOrdering()
    {
        source = new ObservableRangeCollection<int>();
        ObserveOrdering();
    }

    [GlobalSetup]
    public void Setup()
    {
        observer = new CollectionObserver();
        descending = new int[ElementCount];
        for (var i = 0; i < ElementCount; ++i)
            descending[i] = ElementCount - i;
    }
}
