namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what keeping a copy of an observed sequence adds to a change of that sequence, which is moving the copy's elements after the one inserted or removed
/// </summary>
/// <remarks>
/// Each arm inserts an element into ten thousand and removes it again, at the front or at the end. The unobserved arms price the sequence's own work, which moves its elements the same way the copy's are moved; the observed arms add an observation of the sequence and its copy and nothing else; the filtered arms add a filtered view over that observation, whose own work also grows with the elements after the change. What the copy costs is the observed arms' difference between the front and the end, less the unobserved arms' difference
/// </remarks>
[MemoryDiagnoser]
public class ObservedSequenceCopyBenchmarks
{
    const int elementCount = 10000;

    static readonly Expression<Func<BenchmarkPerson, bool>> rankIsPositive = person => person.Rank > 0;

    readonly BenchmarkPerson inserted = new("inserted", 1);
    IObservableCollectionQuery<BenchmarkPerson> observed = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson>? where;

    [GlobalCleanup(Targets = [nameof(InsertAndRemoveAtEndFiltered), nameof(InsertAndRemoveAtEndObserved), nameof(InsertAndRemoveAtFrontFiltered), nameof(InsertAndRemoveAtFrontObserved)])]
    public void CleanupObserved()
    {
        where?.Dispose();
        observed.Dispose();
    }

    void InsertAndRemove(int index)
    {
        source.Insert(index, inserted);
        source.RemoveAt(index);
    }

    [Benchmark]
    public void InsertAndRemoveAtEndFiltered() =>
        InsertAndRemove(elementCount);

    [Benchmark]
    public void InsertAndRemoveAtEndObserved() =>
        InsertAndRemove(elementCount);

    /// <summary>
    /// What inserting and removing at the end costs before anything observes the sequence, which is the floor the other arms stand on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void InsertAndRemoveAtEndUnobserved() =>
        InsertAndRemove(elementCount);

    [Benchmark]
    public void InsertAndRemoveAtFrontFiltered() =>
        InsertAndRemove(0);

    [Benchmark]
    public void InsertAndRemoveAtFrontObserved() =>
        InsertAndRemove(0);

    [Benchmark]
    public void InsertAndRemoveAtFrontUnobserved() =>
        InsertAndRemove(0);

    [GlobalSetup(Targets = [nameof(InsertAndRemoveAtEndFiltered), nameof(InsertAndRemoveAtFrontFiltered)])]
    public void SetupFiltered()
    {
        SetupObserved();
        where = observed.ObserveWhere(rankIsPositive);
    }

    [GlobalSetup(Targets = [nameof(InsertAndRemoveAtEndObserved), nameof(InsertAndRemoveAtFrontObserved)])]
    public void SetupObserved()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        observed = new CollectionObserver().ObserveReadOnlyList(source);
    }

    [GlobalSetup(Targets = [nameof(InsertAndRemoveAtEndUnobserved), nameof(InsertAndRemoveAtFrontUnobserved)])]
    public void SetupUnobserved() =>
        source = BenchmarkPerson.CreateCollection(elementCount);
}
