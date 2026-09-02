namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class GraphResultBoxingBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> comparison = person => (person.Rank & 1) == 0;
    static readonly Expression<Func<BenchmarkPerson, int>> integer = person => person.Rank & 1;
    static readonly Expression<Func<BenchmarkPerson, bool>> negatedComparison = person => !((person.Rank & 1) == 0);

    IObservableExpression<BenchmarkPerson, bool>[] booleanObservations = null!;
    IObservableExpression<BenchmarkPerson, int>[] integerObservations = null!;
    ExpressionObserver observer = null!;
    BenchmarkPerson[] people = null!;

    [GlobalCleanup(Targets = [nameof(FlipEveryRankObservedAsAComparison), nameof(FlipEveryRankObservedAsANegatedComparison)])]
    public void CleanupBooleanObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            booleanObservations[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(FlipEveryRankObservedAsAnInteger))]
    public void CleanupIntegerObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            integerObservations[i].Dispose();
    }

    void Flip()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void FlipEveryRankObservedAsAComparison() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedAsANegatedComparison() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedAsAnInteger() =>
        Flip();

    [Benchmark(Baseline = true)]
    public void FlipEveryRankWithNothingObserving() =>
        Flip();

    [GlobalSetup(Target = nameof(FlipEveryRankWithNothingObserving))]
    public void SetupBare() =>
        SetupPeople();

    void SetupBooleanObservations(Expression<Func<BenchmarkPerson, bool>> predicate)
    {
        SetupPeople();
        booleanObservations = new IObservableExpression<BenchmarkPerson, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            booleanObservations[i] = observer.ObserveWithoutOptimization(predicate, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankObservedAsAComparison))]
    public void SetupComparison() =>
        SetupBooleanObservations(comparison);

    [GlobalSetup(Target = nameof(FlipEveryRankObservedAsAnInteger))]
    public void SetupInteger()
    {
        SetupPeople();
        integerObservations = new IObservableExpression<BenchmarkPerson, int>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            integerObservations[i] = observer.ObserveWithoutOptimization(integer, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankObservedAsANegatedComparison))]
    public void SetupNegatedComparison() =>
        SetupBooleanObservations(negatedComparison);

    void SetupPeople()
    {
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson($"P{i}", i);
    }
}
