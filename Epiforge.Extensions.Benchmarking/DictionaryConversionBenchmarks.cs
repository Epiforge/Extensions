namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Measures a live dictionary built from a collection with <c>ObserveToDictionary</c>, on standing it up and on propagating a change to every element's value through it
/// </summary>
/// <remarks>
/// Each element's value is its rank, which is exclusive-ored with 2, so every one of the thousand values changes on every round while every key stays where it is. That is the change a dictionary view hears most: an element it already maps changing what it maps to
/// </remarks>
/// <remarks>
/// The standing arm is verified before it is measured, because an arrangement which quietly stopped following its source would be reported as a very fast one. The probe changes an element's value and requires the dictionary to map the element's key to the new one
/// </remarks>
[AgainstReleasedExpressions("7.0.1")]
[MemoryDiagnoser]
public class DictionaryConversionBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, string>> hoistedKeySelector = person => person.Name;
    static readonly Expression<Func<BenchmarkPerson, int>> hoistedValueSelector = person => person.Rank;

    IObservableDictionaryQuery<string, int> dictionary = null!;
    CollectionObserver observer = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;

    void ChangeEveryRank()
    {
        for (var i = 0; i < elementCount; ++i)
            source[i].Rank ^= 2;
    }

    /// <summary>
    /// What changing every element's rank costs before anything observes it, which is the floor the arm below stands on
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ChangeEveryRankUnobserved() =>
        ChangeEveryRank();

    [Benchmark]
    public void ChangeEveryRankWithExpressions() =>
        ChangeEveryRank();

    [Benchmark]
    public void ConstructAndDisposeWithExpressions()
    {
        var query = observer.ObserveReadOnlyList(source);
        var converted = query.ObserveToDictionary(hoistedKeySelector, hoistedValueSelector);
        converted.Dispose();
        query.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void CleanupStandingExpressions()
    {
        dictionary.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalSetup(Target = nameof(ConstructAndDisposeWithExpressions))]
    public void SetupConstructExpressions()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        observer = new CollectionObserver();
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankWithExpressions))]
    public void SetupStandingExpressions()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        dictionary = sourceQuery.ObserveToDictionary(hoistedKeySelector, hoistedValueSelector);
        if (dictionary.Count != elementCount)
            throw new InvalidOperationException($"this library's dictionary maps {dictionary.Count} keys where the source has {elementCount} elements");
        var subject = source[0];
        var held = subject.Rank;
        subject.Rank = held + 1;
        if (!dictionary.TryGetValue(subject.Name, out var changed) || changed != held + 1)
            throw new InvalidOperationException("this library's dictionary did not map an element's key to the value it changed to; this instrument would otherwise be reporting an arrangement which does not do the work as though it did it quickly");
        subject.Rank = held;
    }

    [GlobalSetup(Target = nameof(ChangeEveryRankUnobserved))]
    public void SetupUnobserved() =>
        source = BenchmarkPerson.CreateCollection(elementCount);
}
