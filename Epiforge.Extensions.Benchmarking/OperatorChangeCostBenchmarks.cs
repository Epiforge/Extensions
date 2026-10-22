namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices one change made to a source of a thousand people as each operator follows it, so that what an operator allocates and spends per change of its own can be read against the same change observed by the source alone
/// </summary>
/// <remarks>
/// Each arm builds only what it measures, since every query built over the source would otherwise follow every change made to it. The two source arms are the floors: a query over a source keeps a copy of it, so every other arm includes what following the change into that copy costs
/// </remarks>
[MemoryDiagnoser]
public class OperatorChangeCostBenchmarks
{
    const int elementCount = 1000;

    BenchmarkPerson added = null!;
    string[] alternateNames = null!;
    int cursor;
    CollectionObserver observer = null!;
    string[] originalNames = null!;
    BenchmarkPerson[] pair = null!;
    IDisposable query = null!;
    BenchmarkPerson replacement = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> takeQuery = null!;
    ObservableRangeCollection<BenchmarkTeam> teams = null!;
    IObservableCollectionQuery<BenchmarkTeam> teamsQuery = null!;

    [Benchmark]
    public void AllTogglingItsResult()
    {
        source[0].Rank ^= 1;
        source[0].Rank ^= 1;
    }

    [Benchmark]
    public void AverageAfterAValueChange() =>
        Next().Rank ^= 2;

    [GlobalCleanup]
    public void Cleanup()
    {
        query?.Dispose();
        takeQuery?.Dispose();
        sourceQuery?.Dispose();
        teamsQuery?.Dispose();
    }

    BenchmarkPerson Next()
    {
        var person = source[cursor];
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;
        return person;
    }

    [Benchmark]
    public void OrderByAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark]
    public void SelectAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark]
    public void SelectManyAfterAnInnerReplacement()
    {
        var people = teams[cursor].People;
        cursor = cursor + 1 == elementCount ? 0 : cursor + 1;
        var original = people[0];
        people[0] = replacement;
        people[0] = original;
    }

    [GlobalSetup(Target = nameof(AllTogglingItsResult))]
    public void SetUpAll()
    {
        SetUpSource(i => i == 0 ? 1 : i * 2);
        query = sourceQuery.ObserveAll(person => person.Rank % 2 == 0);
    }

    [GlobalSetup(Target = nameof(AverageAfterAValueChange))]
    public void SetUpAverage()
    {
        SetUpSource(i => i);
        query = sourceQuery.ObserveAverage(person => (double)person.Rank);
    }

    [GlobalSetup(Target = nameof(OrderByAfterAnAdditionAndARemoval))]
    public void SetUpOrderBy()
    {
        SetUpSource(i => i);
        query = sourceQuery.ObserveOrderBy(person => person.Rank);
    }

    [GlobalSetup(Target = nameof(SelectAfterAnAdditionAndARemoval))]
    public void SetUpSelect()
    {
        SetUpSource(i => i);
        query = sourceQuery.ObserveSelect(person => person.Rank);
    }

    [GlobalSetup(Target = nameof(SelectManyAfterAnInnerReplacement))]
    public void SetUpSelectMany()
    {
        observer = new CollectionObserver();
        var teamList = new List<BenchmarkTeam>(elementCount);
        for (var i = 0; i < elementCount; ++i)
            teamList.Add(new BenchmarkTeam(BenchmarkPerson.CreateCollection(4)));
        teams = new ObservableRangeCollection<BenchmarkTeam>(teamList);
        teamsQuery = observer.ObserveReadOnlyList(teams);
        query = teamsQuery.ObserveSelectMany(team => team.People);
        replacement = new BenchmarkPerson("Replacement", 0);
    }

    void SetUpSource(Func<int, int> rank)
    {
        observer = new CollectionObserver();
        var people = new List<BenchmarkPerson>(elementCount);
        for (var i = 0; i < elementCount; ++i)
            people.Add(new BenchmarkPerson($"P{i}", rank(i)));
        source = new ObservableRangeCollection<BenchmarkPerson>(people);
        sourceQuery = observer.ObserveReadOnlyList(source);
        added = new BenchmarkPerson("Added", 0);
        pair = [new BenchmarkPerson("First of a pair", 0), new BenchmarkPerson("Second of a pair", 0)];
    }

    [GlobalSetup(Targets = [nameof(SourceAfterAnAdditionAndARemoval), nameof(SourceAfterAMoveAcrossIt), nameof(SourceAfterARangeAdditionAndRemoval), nameof(SourceAfterAValueChange)])]
    public void SetUpSourceAlone() =>
        SetUpSource(i => i);

    [GlobalSetup(Target = nameof(SumAfterAValueChange))]
    public void SetUpSum()
    {
        SetUpSource(i => i);
        query = sourceQuery.ObserveSum(person => person.Rank);
    }

    [GlobalSetup(Targets = [nameof(TakeThenSelectAfterAnAdditionAndARemovalBeyondIt), nameof(TakeThenSelectAfterAnInsertionAndARemovalAtItsStart)])]
    public void SetUpTakeThenSelect()
    {
        SetUpSource(i => i);
        takeQuery = sourceQuery.ObserveTake(10);
        query = takeQuery.ObserveSelect(person => person.Rank);
    }

    [GlobalSetup(Targets = [nameof(ToDictionaryAfterAnAdditionAndARemoval), nameof(ToDictionaryAfterAKeyChange)])]
    public void SetUpToDictionary()
    {
        SetUpSource(i => i);
        originalNames = new string[elementCount];
        alternateNames = new string[elementCount];
        for (var i = 0; i < elementCount; ++i)
        {
            originalNames[i] = source[i].Name;
            alternateNames[i] = $"{source[i].Name} renamed";
        }
        query = sourceQuery.ObserveToDictionary(person => person.Name, person => person.Rank);
    }

    [GlobalSetup(Target = nameof(WhereAfterAnAdditionAndARemoval))]
    public void SetUpWhere()
    {
        SetUpSource(i => i);
        query = sourceQuery.ObserveWhere(person => person.Rank % 2 == 0);
    }

    [Benchmark(Baseline = true)]
    public void SourceAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark]
    public void SourceAfterAMoveAcrossIt()
    {
        source.Move(0, elementCount - 1);
        source.Move(elementCount - 1, 0);
    }

    [Benchmark]
    public void SourceAfterARangeAdditionAndRemoval()
    {
        source.AddRange(pair);
        source.RemoveRange(elementCount, 2);
    }

    [Benchmark]
    public void SourceAfterAValueChange() =>
        Next().Rank ^= 2;

    [Benchmark]
    public void SumAfterAValueChange() =>
        Next().Rank ^= 2;

    [Benchmark]
    public void TakeThenSelectAfterAnAdditionAndARemovalBeyondIt()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark]
    public void TakeThenSelectAfterAnInsertionAndARemovalAtItsStart()
    {
        source.Insert(0, added);
        source.RemoveAt(0);
    }

    [Benchmark]
    public void ToDictionaryAfterAKeyChange()
    {
        var index = cursor;
        var person = Next();
        person.Name = alternateNames[index];
        person.Name = originalNames[index];
    }

    [Benchmark]
    public void ToDictionaryAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }

    [Benchmark]
    public void WhereAfterAnAdditionAndARemoval()
    {
        source.Add(added);
        source.RemoveAt(elementCount);
    }
}
