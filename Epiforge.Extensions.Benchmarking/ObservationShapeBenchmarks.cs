namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class ObservationShapeBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>> pairConstantKeyProjection = pair => new KeyValuePair<int, int>(0, pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairMember = pair => pair.Value.Rank;
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairMethodCall = pair => pair.Value.Rank.CompareTo(0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>> pairMethodCallReturningAPair = pair => KeyValuePair.Create(pair.Value.Rank, 0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, (int First, int Second)>> pairMethodCallReturningATuple = pair => ValueTuple.Create(pair.Value.Rank, 0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>> pairProjection = pair => new KeyValuePair<int, int>(pair.Key, pair.Value.Rank);

    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] constantKeyProjections = null!;
    ExpressionObserver expressionObserver = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] memberReads = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] methodCalls = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] methodCallsReturningPairs = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, (int First, int Second)>[] methodCallsReturningTuples = null!;
    BenchmarkPerson[] people = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] projections = null!;

    void ChangeEveryValue()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void ChangeEveryValueMemberRead() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMethodCalled() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMethodCalledForAPair() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMethodCalledForATuple() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjected() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjectedOverAConstantKey() =>
        ChangeEveryValue();

    [Benchmark(Baseline = true)]
    public void ChangeEveryValueWithNoObservation() =>
        ChangeEveryValue();

    [GlobalCleanup(Target = nameof(ChangeEveryValueProjectedOverAConstantKey))]
    public void CleanupConstantKeyProjections()
    {
        for (var i = 0; i < elementCount; ++i)
            constantKeyProjections[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMemberRead))]
    public void CleanupMemberReads()
    {
        for (var i = 0; i < elementCount; ++i)
            memberReads[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMethodCalled))]
    public void CleanupMethodCalls()
    {
        for (var i = 0; i < elementCount; ++i)
            methodCalls[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMethodCalledForAPair))]
    public void CleanupMethodCallsReturningPairs()
    {
        for (var i = 0; i < elementCount; ++i)
            methodCallsReturningPairs[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMethodCalledForATuple))]
    public void CleanupMethodCallsReturningTuples()
    {
        for (var i = 0; i < elementCount; ++i)
            methodCallsReturningTuples[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueProjected))]
    public void CleanupProjections()
    {
        for (var i = 0; i < elementCount; ++i)
            projections[i].Dispose();
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueWithNoObservation))]
    public void SetupBare() =>
        SetupPeople();

    [GlobalSetup(Target = nameof(ChangeEveryValueProjectedOverAConstantKey))]
    public void SetupConstantKeyProjections()
    {
        SetupObserver();
        constantKeyProjections = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            constantKeyProjections[i] = expressionObserver.ObserveWithoutOptimization(pairConstantKeyProjection, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMemberRead))]
    public void SetupMemberReads()
    {
        SetupObserver();
        memberReads = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            memberReads[i] = expressionObserver.ObserveWithoutOptimization(pairMember, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMethodCalled))]
    public void SetupMethodCalls()
    {
        SetupObserver();
        methodCalls = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            methodCalls[i] = expressionObserver.ObserveWithoutOptimization(pairMethodCall, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMethodCalledForAPair))]
    public void SetupMethodCallsReturningPairs()
    {
        SetupObserver();
        methodCallsReturningPairs = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            methodCallsReturningPairs[i] = expressionObserver.ObserveWithoutOptimization(pairMethodCallReturningAPair, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMethodCalledForATuple))]
    public void SetupMethodCallsReturningTuples()
    {
        SetupObserver();
        methodCallsReturningTuples = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, (int First, int Second)>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            methodCallsReturningTuples[i] = expressionObserver.ObserveWithoutOptimization(pairMethodCallReturningATuple, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    void SetupObserver()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver();
    }

    void SetupPeople()
    {
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i);
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueProjected))]
    public void SetupProjections()
    {
        SetupObserver();
        projections = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            projections[i] = expressionObserver.ObserveWithoutOptimization(pairProjection, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }
}
