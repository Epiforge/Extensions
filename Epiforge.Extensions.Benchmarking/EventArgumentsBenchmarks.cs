namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class EventArgumentsBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairMember = pair => pair.Value.Rank;
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPersonWithCachedEventArguments>, int>> cachedPairMember = pair => pair.Value.Rank;

    ExpressionObserver expressionObserver = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPersonWithCachedEventArguments>, int>[] cachedMemberReads = null!;
    BenchmarkPersonWithCachedEventArguments[] cachedPeople = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] memberReads = null!;
    BenchmarkPerson[] people = null!;

    void ChangeEveryCachedValue()
    {
        for (var i = 0; i < elementCount; ++i)
            cachedPeople[i].Rank ^= 1;
    }

    void ChangeEveryValue()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void ChangeEveryValueMemberRead() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMemberReadWithCachedEventArguments() =>
        ChangeEveryCachedValue();

    [Benchmark(Baseline = true)]
    public void ChangeEveryValueWithNoObservation() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueWithNoObservationAndCachedEventArguments() =>
        ChangeEveryCachedValue();

    [GlobalCleanup(Target = nameof(ChangeEveryValueMemberReadWithCachedEventArguments))]
    public void CleanupCachedMemberReads()
    {
        for (var i = 0; i < elementCount; ++i)
            cachedMemberReads[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMemberRead))]
    public void CleanupMemberReads()
    {
        for (var i = 0; i < elementCount; ++i)
            memberReads[i].Dispose();
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueWithNoObservationAndCachedEventArguments))]
    public void SetupBareCachedPeople() =>
        SetupCachedPeople();

    [GlobalSetup(Target = nameof(ChangeEveryValueWithNoObservation))]
    public void SetupBarePeople() =>
        SetupPeople();

    void SetupCachedPeople()
    {
        cachedPeople = new BenchmarkPersonWithCachedEventArguments[elementCount];
        for (var i = 0; i < elementCount; ++i)
            cachedPeople[i] = new BenchmarkPersonWithCachedEventArguments(i);
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMemberReadWithCachedEventArguments))]
    public void SetupCachedMemberReads()
    {
        SetupCachedPeople();
        SetupObserver();
        cachedMemberReads = new IObservableExpression<KeyValuePair<int, BenchmarkPersonWithCachedEventArguments>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            cachedMemberReads[i] = expressionObserver.ObserveWithoutOptimization(cachedPairMember, new KeyValuePair<int, BenchmarkPersonWithCachedEventArguments>(i, cachedPeople[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMemberRead))]
    public void SetupMemberReads()
    {
        SetupPeople();
        SetupObserver();
        memberReads = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            memberReads[i] = expressionObserver.ObserveWithoutOptimization(pairMember, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    void SetupObserver() =>
        expressionObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });

    void SetupPeople()
    {
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i);
    }
}
