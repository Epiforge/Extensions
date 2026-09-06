namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class StructResultBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> intResult = pair => pair.Value.Rank.CompareTo(0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, long>> longResult = pair => Convert.ToInt64(pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, OneFieldStruct>> oneFieldResult = pair => OneFieldStruct.Create(pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, ThreeFieldStruct>> threeFieldResult = pair => ThreeFieldStruct.Create(pair.Value.Rank, 0, 0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, TwoFieldStruct>> twoFieldResult = pair => TwoFieldStruct.Create(pair.Value.Rank, 0);

    ExpressionObserver expressionObserver = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] intResults = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, long>[] longResults = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, OneFieldStruct>[] oneFieldResults = null!;
    BenchmarkPerson[] people = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, ThreeFieldStruct>[] threeFieldResults = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, TwoFieldStruct>[] twoFieldResults = null!;

    void ChangeEveryValue()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void ChangeEveryValueForAnInt() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueForALong() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueForOneField() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueForThreeFields() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueForTwoFields() =>
        ChangeEveryValue();

    [Benchmark(Baseline = true)]
    public void ChangeEveryValueWithNoObservation() =>
        ChangeEveryValue();

    [GlobalCleanup(Target = nameof(ChangeEveryValueForALong))]
    public void CleanupLongResults()
    {
        for (var i = 0; i < elementCount; ++i)
            longResults[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueForAnInt))]
    public void CleanupIntResults()
    {
        for (var i = 0; i < elementCount; ++i)
            intResults[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueForOneField))]
    public void CleanupOneFieldResults()
    {
        for (var i = 0; i < elementCount; ++i)
            oneFieldResults[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueForThreeFields))]
    public void CleanupThreeFieldResults()
    {
        for (var i = 0; i < elementCount; ++i)
            threeFieldResults[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueForTwoFields))]
    public void CleanupTwoFieldResults()
    {
        for (var i = 0; i < elementCount; ++i)
            twoFieldResults[i].Dispose();
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueWithNoObservation))]
    public void SetupBare() =>
        SetupPeople();

    [GlobalSetup(Target = nameof(ChangeEveryValueForAnInt))]
    public void SetupIntResults()
    {
        SetupObserver();
        intResults = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            intResults[i] = expressionObserver.ObserveWithoutOptimization(intResult, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueForALong))]
    public void SetupLongResults()
    {
        SetupObserver();
        longResults = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, long>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            longResults[i] = expressionObserver.ObserveWithoutOptimization(longResult, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    void SetupObserver()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueForOneField))]
    public void SetupOneFieldResults()
    {
        SetupObserver();
        oneFieldResults = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, OneFieldStruct>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            oneFieldResults[i] = expressionObserver.ObserveWithoutOptimization(oneFieldResult, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    void SetupPeople()
    {
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i);
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueForThreeFields))]
    public void SetupThreeFieldResults()
    {
        SetupObserver();
        threeFieldResults = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, ThreeFieldStruct>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            threeFieldResults[i] = expressionObserver.ObserveWithoutOptimization(threeFieldResult, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueForTwoFields))]
    public void SetupTwoFieldResults()
    {
        SetupObserver();
        twoFieldResults = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, TwoFieldStruct>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            twoFieldResults[i] = expressionObserver.ObserveWithoutOptimization(twoFieldResult, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }
}
