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
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairMethodCallWithNoArguments = pair => pair.Value.Rank.GetHashCode();
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairStaticMethodCall = pair => Math.Abs(pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, int>> pairTwoMethodCalls = pair => pair.Value.Rank.CompareTo(0).CompareTo(0);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>> pairProjection = pair => new KeyValuePair<int, int>(pair.Key, pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>> pairConstruction = pair => new BenchmarkBox(pair.Value.Rank);
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>> pairMemberInitialization = pair => new BenchmarkBox { First = pair.Value.Rank };

    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] constantKeyProjections = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[] constructions = null!;
    ExpressionObserver expressionObserver = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[] fastPathConstructions = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[] fastPathMemberInitializations = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] fastPathProjections = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[] memberInitializations = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] memberReads = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] methodCalls = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] methodCallsWithNoArguments = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] staticMethodCalls = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[] twoMethodCalls = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] methodCallsReturningPairs = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, (int First, int Second)>[] methodCallsReturningTuples = null!;
    BenchmarkPerson[] people = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] projections = null!;

    void ChangeEveryValue()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    /// <summary>
    /// Constructing a reference type per change, with and without a member initializer, on each mechanism
    /// </summary>
    /// <remarks>
    /// A member initialization used to write a changed binding into the object it had already produced, which allocated nothing further and announced nothing at all. It now constructs, so the excess of these arms over the member read is what that correction costs per notification, and the excess of the initialized arm over the constructed one is what the binding writes cost
    /// </remarks>
    [Benchmark]
    public void ChangeEveryValueConstructed() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueConstructedWithDirectSubscriptionAllowed() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMemberInitialized() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueMemberInitializedWithDirectSubscriptionAllowed() =>
        ChangeEveryValue();

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

    /// <summary>
    /// An instance call taking no arguments, a static call taking one, and two calls in a row, against the one instance call taking one argument already here, so that the 24 bytes a call costs over a member read can be told from what varies between them
    /// </summary>
    [Benchmark]
    public void ChangeEveryValueMethodCalledWithNoArguments() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjected() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjectedOverAConstantKey() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjectedWithDirectSubscriptionAllowed() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueStaticMethodCalled() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueTwoMethodsCalled() =>
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

    [GlobalCleanup(Target = nameof(ChangeEveryValueConstructed))]
    public void CleanupConstructions()
    {
        for (var i = 0; i < elementCount; ++i)
            constructions[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueConstructedWithDirectSubscriptionAllowed))]
    public void CleanupFastPathConstructions()
    {
        for (var i = 0; i < elementCount; ++i)
            fastPathConstructions[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMemberInitializedWithDirectSubscriptionAllowed))]
    public void CleanupFastPathMemberInitializations()
    {
        for (var i = 0; i < elementCount; ++i)
            fastPathMemberInitializations[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueProjectedWithDirectSubscriptionAllowed))]
    public void CleanupFastPathProjections()
    {
        for (var i = 0; i < elementCount; ++i)
            fastPathProjections[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueMemberInitialized))]
    public void CleanupMemberInitializations()
    {
        for (var i = 0; i < elementCount; ++i)
            memberInitializations[i].Dispose();
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

    [GlobalCleanup(Target = nameof(ChangeEveryValueMethodCalledWithNoArguments))]
    public void CleanupMethodCallsWithNoArguments()
    {
        for (var i = 0; i < elementCount; ++i)
            methodCallsWithNoArguments[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueProjected))]
    public void CleanupProjections()
    {
        for (var i = 0; i < elementCount; ++i)
            projections[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueStaticMethodCalled))]
    public void CleanupStaticMethodCalls()
    {
        for (var i = 0; i < elementCount; ++i)
            staticMethodCalls[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueTwoMethodsCalled))]
    public void CleanupTwoMethodCalls()
    {
        for (var i = 0; i < elementCount; ++i)
            twoMethodCalls[i].Dispose();
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

    [GlobalSetup(Target = nameof(ChangeEveryValueConstructed))]
    public void SetupConstructions()
    {
        SetupObserver();
        constructions = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            constructions[i] = expressionObserver.ObserveWithoutOptimization(pairConstruction, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueConstructedWithDirectSubscriptionAllowed))]
    public void SetupFastPathConstructions()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver();
        fastPathConstructions = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            fastPathConstructions[i] = expressionObserver.ObserveWithoutOptimization(pairConstruction, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMemberInitializedWithDirectSubscriptionAllowed))]
    public void SetupFastPathMemberInitializations()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver();
        fastPathMemberInitializations = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            fastPathMemberInitializations[i] = expressionObserver.ObserveWithoutOptimization(pairMemberInitialization, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueProjectedWithDirectSubscriptionAllowed))]
    public void SetupFastPathProjections()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver();
        fastPathProjections = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            fastPathProjections[i] = expressionObserver.ObserveWithoutOptimization(pairProjection, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueMemberInitialized))]
    public void SetupMemberInitializations()
    {
        SetupObserver();
        memberInitializations = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, BenchmarkBox>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            memberInitializations[i] = expressionObserver.ObserveWithoutOptimization(pairMemberInitialization, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
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

    [GlobalSetup(Target = nameof(ChangeEveryValueMethodCalledWithNoArguments))]
    public void SetupMethodCallsWithNoArguments()
    {
        SetupObserver();
        methodCallsWithNoArguments = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            methodCallsWithNoArguments[i] = expressionObserver.ObserveWithoutOptimization(pairMethodCallWithNoArguments, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    void SetupObserver()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
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

    [GlobalSetup(Target = nameof(ChangeEveryValueStaticMethodCalled))]
    public void SetupStaticMethodCalls()
    {
        SetupObserver();
        staticMethodCalls = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            staticMethodCalls[i] = expressionObserver.ObserveWithoutOptimization(pairStaticMethodCall, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueTwoMethodsCalled))]
    public void SetupTwoMethodCalls()
    {
        SetupObserver();
        twoMethodCalls = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            twoMethodCalls[i] = expressionObserver.ObserveWithoutOptimization(pairTwoMethodCalls, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }
}
