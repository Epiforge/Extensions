namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a cast over a thousand people as a query is built, disposed and changed through it, alone and in a chain shaped like the one GS5 sums a person's time through, against a projection doing the same conversion, and a type filter over a thousand elements of which half pass it, with a cast and a type filter over dictionaries of a thousand values built and disposed likewise, and each under an aggregate as a change reaches it
/// </summary>
/// <remarks>
/// The chain converts each person to <see cref="object"/> and back, projects the rank, converts that to <see cref="object"/> and back to <see cref="int"/> and sums it, which is the shape of GS5's person total: a cast either side of a projection, twice over, and an aggregate at the end. The projection arm uses a selector with the same body as the cast's, so that what a cast pays over what a conversion needs can be read from the difference. The type filter tests for an interface, as GS5's do, over people alternating with text. The aggregate arms are shaped like GS5's rate sum, a cast of projected values under an aggregate, which reads every element of what it is over after every change, so that what reading a cast or a type filter costs once it has changed is priced
/// </remarks>
[MemoryDiagnoser]
public class CastCostBenchmarks
{
    const int elementCount = 1000;

    BenchmarkPerson added = null!;
    int cursor;
    BenchmarkPerson displaced = null!;
    ObservableRangeCollection<object> mixed = null!;
    IObservableDictionaryQuery<int, object> mixedByIndexQuery = null!;
    IObservableCollectionQuery<object> mixedQuery = null!;
    CollectionObserver observer = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> peopleByIndexQuery = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IDisposable standing = null!;

    [Benchmark]
    public void AggregateOverACastAfterAValueChange()
    {
        var person = source[cursor];
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;
        person.Rank ^= 2;
    }

    [Benchmark]
    public void AggregateOverATypeFilterAfterAReplacement() =>
        mixed[0] = mixed[0] == added ? displaced : added;

    [Benchmark]
    public void CastAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark(Baseline = true)]
    public void CastBuiltAndDisposed() =>
        sourceQuery.ObserveCast<object>().Dispose();

    [Benchmark]
    public void ChainAfterAValueChange()
    {
        var person = source[cursor];
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;
        person.Rank ^= 2;
    }

    [Benchmark]
    public void ChainBuiltAndDisposed() =>
        Chain().Dispose();

    IDisposable Chain()
    {
        var people = sourceQuery.ObserveCast<object>();
        var cast = people.ObserveCast<BenchmarkPerson>();
        var ranks = cast.ObserveSelect(person => person.Rank);
        var boxed = ranks.ObserveCast<object>();
        var unboxed = boxed.ObserveCast<int>();
        var total = unboxed.ObserveSum(rank => rank);
        total.Disposed += (sender, e) =>
        {
            unboxed.Dispose();
            boxed.Dispose();
            ranks.Dispose();
            cast.Dispose();
            people.Dispose();
        };
        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        standing?.Dispose();
        sourceQuery?.Dispose();
        mixedQuery?.Dispose();
        mixedByIndexQuery?.Dispose();
        peopleByIndexQuery?.Dispose();
    }

    [Benchmark]
    public void DictionaryCastBuiltAndDisposed() =>
        peopleByIndexQuery.ObserveCast<object>().Dispose();

    [Benchmark]
    public void DictionaryOfTypeBuiltAndDisposed() =>
        mixedByIndexQuery.ObserveOfType<INotifyPropertyChanged>().Dispose();

    [Benchmark]
    public void OfTypeAfterAnAdditionAndARemoval()
    {
        mixed.Add(added);
        mixed.RemoveAt(elementCount);
    }

    [Benchmark]
    public void OfTypeBuiltAndDisposed() =>
        mixedQuery.ObserveOfType<INotifyPropertyChanged>().Dispose();

    [Benchmark]
    public void ProjectionBuiltAndDisposed() =>
        sourceQuery.ObserveSelect(person => (object)person).Dispose();

    void SetUpSource()
    {
        observer = new CollectionObserver();
        var people = new List<BenchmarkPerson>(elementCount);
        for (var i = 0; i < elementCount; ++i)
            people.Add(new BenchmarkPerson($"P{i}", i));
        source = new ObservableRangeCollection<BenchmarkPerson>(people);
        sourceQuery = observer.ObserveReadOnlyList(source);
        var alternating = new List<object>(elementCount);
        for (var i = 0; i < elementCount / 2; ++i)
        {
            alternating.Add(people[i]);
            alternating.Add($"T{i}");
        }
        mixed = new ObservableRangeCollection<object>(alternating);
        mixedQuery = observer.ObserveReadOnlyList(mixed);
        var byIndex = new Dictionary<int, BenchmarkPerson>(elementCount);
        var alternatingByIndex = new Dictionary<int, object>(elementCount);
        for (var i = 0; i < elementCount; ++i)
        {
            byIndex.Add(i, people[i]);
            alternatingByIndex.Add(i, alternating[i]);
        }
        peopleByIndexQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<int, BenchmarkPerson>(byIndex));
        mixedByIndexQuery = observer.ObserveReadOnlyDictionary(new ObservableDictionary<int, object>(alternatingByIndex));
        added = new BenchmarkPerson("Added", 0);
        displaced = people[0];
    }

    [GlobalSetup(Target = nameof(AggregateOverACastAfterAValueChange))]
    public void SetUpAggregateOverACast()
    {
        SetUpSource();
        var ranks = sourceQuery.ObserveSelect(person => (object)person.Rank);
        var cast = ranks.ObserveCast<int>();
        var total = cast.ObserveAggregate(() => 0L, (sum, rank) => sum + rank, sum => sum);
        total.Disposed += (sender, e) =>
        {
            cast.Dispose();
            ranks.Dispose();
        };
        standing = total;
    }

    [GlobalSetup(Target = nameof(AggregateOverATypeFilterAfterAReplacement))]
    public void SetUpAggregateOverATypeFilter()
    {
        SetUpSource();
        var people = mixedQuery.ObserveOfType<BenchmarkPerson>();
        var total = people.ObserveAggregate(() => 0L, (sum, person) => sum + person.Rank, sum => sum);
        total.Disposed += (sender, e) =>
            people.Dispose();
        standing = total;
    }

    [GlobalSetup(Targets = [nameof(CastBuiltAndDisposed), nameof(ChainBuiltAndDisposed), nameof(DictionaryCastBuiltAndDisposed), nameof(DictionaryOfTypeBuiltAndDisposed), nameof(OfTypeBuiltAndDisposed), nameof(ProjectionBuiltAndDisposed)])]
    public void SetUpBuilding() =>
        SetUpSource();

    [GlobalSetup(Target = nameof(CastAfterAnAdditionAndARemoval))]
    public void SetUpCast()
    {
        SetUpSource();
        standing = sourceQuery.ObserveCast<object>();
    }

    [GlobalSetup(Target = nameof(ChainAfterAValueChange))]
    public void SetUpChain()
    {
        SetUpSource();
        standing = Chain();
    }

    [GlobalSetup(Target = nameof(OfTypeAfterAnAdditionAndARemoval))]
    public void SetUpOfType()
    {
        SetUpSource();
        standing = mixedQuery.ObserveOfType<INotifyPropertyChanged>();
    }
}
