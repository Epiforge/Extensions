namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices one change to a dictionary of ten thousand people as a projection of it follows the change, against the same change observed by the dictionary's own query alone
/// </summary>
/// <remarks>
/// The released job is the one in which the projection reconsiders every key's fault on each change made to the dictionary, so the difference between the jobs on the arms which add, remove or replace a key is what no longer doing so is worth, at a size where it shows
/// </remarks>
[AgainstReleasedExpressions("7.1.3")]
[MemoryDiagnoser]
public class DictionarySelectChurnBenchmarks
{
    const int elementCount = 10000;

    static readonly Expression<Func<int, BenchmarkPerson, int>> keySelector = (key, person) => key;
    static readonly Expression<Func<int, BenchmarkPerson, int>> valueSelector = (key, person) => person.Rank;

    BenchmarkPerson added = null!;
    int cursor;
    CollectionObserver observer = null!;
    BenchmarkPerson[] people = null!;
    BenchmarkPerson replacement = null!;
    IObservableDictionaryQuery<int, int>? select;
    ObservableDictionary<int, BenchmarkPerson> source = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> sourceQuery = null!;

    void AddAndRemoveAKey()
    {
        source.Add(elementCount, added);
        source.Remove(elementCount);
    }

    [Benchmark]
    public void AddAndRemoveAKeyObservedByTheDictionaryQuery() =>
        AddAndRemoveAKey();

    [Benchmark]
    public void AddAndRemoveAKeyProjected() =>
        AddAndRemoveAKey();

    void ChangeAValue() =>
        people[NextKey()].Rank ^= 1;

    [Benchmark]
    public void ChangeAValueObservedByTheDictionaryQuery() =>
        ChangeAValue();

    [Benchmark]
    public void ChangeAValueProjected() =>
        ChangeAValue();

    [GlobalCleanup]
    public void Cleanup()
    {
        select?.Dispose();
        sourceQuery.Dispose();
    }

    int NextKey()
    {
        var key = cursor;
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;
        return key;
    }

    void ReplaceAValue()
    {
        var key = NextKey();
        source[key] = replacement;
        source[key] = people[key];
    }

    [Benchmark]
    public void ReplaceAValueObservedByTheDictionaryQuery() =>
        ReplaceAValue();

    [Benchmark]
    public void ReplaceAValueProjected() =>
        ReplaceAValue();

    void SetUpDictionary()
    {
        observer = new CollectionObserver();
        people = new BenchmarkPerson[elementCount];
        source = new();
        for (var i = 0; i < elementCount; ++i)
            source.Add(i, people[i] = new BenchmarkPerson($"P{i}", i));
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        added = new BenchmarkPerson("Added", 0);
        replacement = new BenchmarkPerson("Replacement", 0);
    }

    [GlobalSetup(Targets = [nameof(AddAndRemoveAKeyObservedByTheDictionaryQuery), nameof(ChangeAValueObservedByTheDictionaryQuery), nameof(ReplaceAValueObservedByTheDictionaryQuery)])]
    public void SetUpDictionaryQuery() =>
        SetUpDictionary();

    [GlobalSetup(Targets = [nameof(AddAndRemoveAKeyProjected), nameof(ChangeAValueProjected), nameof(ReplaceAValueProjected)])]
    public void SetUpProjection()
    {
        SetUpDictionary();
        select = sourceQuery.ObserveSelect(keySelector, valueSelector);
    }
}
