namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices a change to the argument of a thousand standing graph observations of a static call, by the number of arguments the call takes, against the same changes observed by nothing
/// </summary>
/// <remarks>
/// The observer is told not to subscribe directly, so that every arm is observed by a graph of nodes. Each call takes the changing rank and constants for its other arguments, so that each change re-evaluates the rank's node and the call's node and nothing else; what separates the arms is how the call's node hands its arguments to the invoker
/// </remarks>
[MemoryDiagnoser]
public class GraphCallArityBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, int>> fourArgumentCall = person => Total(person.Rank, 1, 2, 3);
    static readonly Expression<Func<BenchmarkPerson, int>> threeArgumentCall = person => Total(person.Rank, 1, 2);
    static readonly Expression<Func<BenchmarkPerson, int>> twoArgumentCall = person => Total(person.Rank, 1);

    IObservableExpression<BenchmarkPerson, int>[] observations = null!;
    ExpressionObserver observer = null!;
    BenchmarkPerson[] people = null!;

    [GlobalCleanup(Targets = [nameof(FlipEveryRankObservedThroughAFourArgumentCall), nameof(FlipEveryRankObservedThroughAThreeArgumentCall), nameof(FlipEveryRankObservedThroughATwoArgumentCall)])]
    public void CleanupObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            observations[i].Dispose();
    }

    void Flip()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 2;
    }

    [Benchmark]
    public void FlipEveryRankObservedThroughAFourArgumentCall() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedThroughAThreeArgumentCall() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedThroughATwoArgumentCall() =>
        Flip();

    [Benchmark(Baseline = true)]
    public void FlipEveryRankWithNoObservation() =>
        Flip();

    void Observe(Expression<Func<BenchmarkPerson, int>> call)
    {
        SetupPeople();
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        observations = new IObservableExpression<BenchmarkPerson, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = observer.ObserveWithoutOptimization(call, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithNoObservation))]
    public void SetupBare() =>
        SetupPeople();

    [GlobalSetup(Target = nameof(FlipEveryRankObservedThroughAFourArgumentCall))]
    public void SetupFourArgumentCall() =>
        Observe(fourArgumentCall);

    void SetupPeople()
    {
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i * 4);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankObservedThroughAThreeArgumentCall))]
    public void SetupThreeArgumentCall() =>
        Observe(threeArgumentCall);

    [GlobalSetup(Target = nameof(FlipEveryRankObservedThroughATwoArgumentCall))]
    public void SetupTwoArgumentCall() =>
        Observe(twoArgumentCall);

    public static int Total(int first, int second) =>
        first + second;

    public static int Total(int first, int second, int third) =>
        first + second + third;

    public static int Total(int first, int second, int third, int fourth) =>
        first + second + third + fourth;
}
