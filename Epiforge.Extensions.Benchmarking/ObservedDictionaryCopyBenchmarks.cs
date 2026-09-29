namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices what keeping a copy of an observed dictionary adds to a change of that dictionary, which is bringing the copy into agreement with the dictionary for the key the change named
/// </summary>
/// <remarks>
/// Each arm changes one key of ten thousand and changes it back, either adding and removing a key or replacing a key's value and restoring it. The unobserved arms price the dictionary's own work; the observed arms add an observation of the dictionary and its copy and nothing else; the filtered arms add a filtered view over that observation. The concurrent arms add and remove a key of an <see cref="ObservableConcurrentDictionary{TKey, TValue}" />, the dictionary several threads may change at once, from one thread
/// </remarks>
[AgainstReleasedExpressions("7.0.0")]
[MemoryDiagnoser]
public class ObservedDictionaryCopyBenchmarks
{
    const int elementCount = 10000;

    readonly BenchmarkPerson added = new("added", 1);
    ObservableConcurrentDictionary<int, BenchmarkPerson> concurrentSource = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> observed = null!;
    BenchmarkPerson original = null!;
    readonly BenchmarkPerson replacement = new("replacement", 1);
    ObservableDictionary<int, BenchmarkPerson> source = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson>? where;

    void AddAndRemove()
    {
        source.Add(elementCount, added);
        source.Remove(elementCount);
    }

    void AddAndRemoveConcurrently()
    {
        concurrentSource.TryAdd(elementCount, added);
        concurrentSource.TryRemove(elementCount, out _);
    }

    [Benchmark]
    public void AddAndRemoveConcurrentObserved() =>
        AddAndRemoveConcurrently();

    [Benchmark]
    public void AddAndRemoveConcurrentUnobserved() =>
        AddAndRemoveConcurrently();

    [Benchmark]
    public void AddAndRemoveFiltered() =>
        AddAndRemove();

    [Benchmark]
    public void AddAndRemoveObserved() =>
        AddAndRemove();

    /// <summary>
    /// What adding and removing a key costs before anything observes the dictionary, which is the floor the other arms stand on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void AddAndRemoveUnobserved() =>
        AddAndRemove();

    [GlobalCleanup(Targets = [nameof(AddAndRemoveConcurrentObserved), nameof(AddAndRemoveFiltered), nameof(AddAndRemoveObserved), nameof(ReplaceAndRestoreFiltered), nameof(ReplaceAndRestoreObserved)])]
    public void CleanupObserved()
    {
        where?.Dispose();
        observed.Dispose();
    }

    void Probe(IObservableDictionaryQuery<int, BenchmarkPerson> view)
    {
        source.Add(elementCount, added);
        if (!view.TryGetValue(elementCount, out var seen) || seen != added)
            throw new InvalidOperationException("the view did not follow the added key");
        source.Remove(elementCount);
        if (view.ContainsKey(elementCount))
            throw new InvalidOperationException("the view did not follow the removed key");
        source[0] = replacement;
        if (!view.TryGetValue(0, out seen) || seen != replacement)
            throw new InvalidOperationException("the view did not follow the replaced value");
        source[0] = original;
        if (!view.TryGetValue(0, out seen) || seen != original)
            throw new InvalidOperationException("the view did not follow the restored value");
    }

    void ReplaceAndRestore()
    {
        source[0] = replacement;
        source[0] = original;
    }

    [Benchmark]
    public void ReplaceAndRestoreFiltered() =>
        ReplaceAndRestore();

    [Benchmark]
    public void ReplaceAndRestoreObserved() =>
        ReplaceAndRestore();

    [Benchmark]
    public void ReplaceAndRestoreUnobserved() =>
        ReplaceAndRestore();

    [GlobalSetup(Target = nameof(AddAndRemoveConcurrentObserved))]
    public void SetupConcurrentObserved()
    {
        SetupConcurrentUnobserved();
        observed = new CollectionObserver().ObserveReadOnlyDictionary(concurrentSource);
        concurrentSource.TryAdd(elementCount, added);
        if (!observed.TryGetValue(elementCount, out var seen) || seen != added)
            throw new InvalidOperationException("the view did not follow the added key");
        concurrentSource.TryRemove(elementCount, out _);
        if (observed.ContainsKey(elementCount))
            throw new InvalidOperationException("the view did not follow the removed key");
    }

    [GlobalSetup(Target = nameof(AddAndRemoveConcurrentUnobserved))]
    public void SetupConcurrentUnobserved()
    {
        concurrentSource = new ObservableConcurrentDictionary<int, BenchmarkPerson>();
        for (var i = 0; i < elementCount; ++i)
            concurrentSource.TryAdd(i, new BenchmarkPerson($"P{i}", i + 1));
    }

    [GlobalSetup(Targets = [nameof(AddAndRemoveFiltered), nameof(ReplaceAndRestoreFiltered)])]
    public void SetupFiltered()
    {
        SetupObserved();
        where = observed.ObserveWhere((key, person) => person.Rank > 0);
        Probe(where);
    }

    [GlobalSetup(Targets = [nameof(AddAndRemoveObserved), nameof(ReplaceAndRestoreObserved)])]
    public void SetupObserved()
    {
        SetupUnobserved();
        observed = new CollectionObserver().ObserveReadOnlyDictionary(source);
        Probe(observed);
    }

    [GlobalSetup(Targets = [nameof(AddAndRemoveUnobserved), nameof(ReplaceAndRestoreUnobserved)])]
    public void SetupUnobserved()
    {
        source = new ObservableDictionary<int, BenchmarkPerson>();
        for (var i = 0; i < elementCount; ++i)
            source.Add(i, new BenchmarkPerson($"P{i}", i + 1));
        original = source[0];
    }
}
