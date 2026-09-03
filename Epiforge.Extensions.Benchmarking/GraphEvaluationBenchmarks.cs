namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class GraphEvaluationBenchmarks
{
    const int elementCount = 1000;
    const string longName = "abc";
    const string shortName = "a";

    static readonly Expression<Func<BenchmarkPerson, bool>> memberComparison = person => person.Name.Length > 0;
    static readonly Expression<Func<BenchmarkPerson, bool>> methodCall = person => string.IsNullOrEmpty(person.Name);

    bool alternate;
    IObservableExpression<BenchmarkPerson, bool>[] observations = null!;
    ExpressionObserver observer = null!;
    BenchmarkPerson[] people = null!;

    [GlobalCleanup(Targets = [nameof(FlipEveryNameObservedAsAMemberComparison), nameof(FlipEveryNameObservedAsAMethodCall)])]
    public void CleanupObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            observations[i].Dispose();
    }

    void Flip()
    {
        alternate = !alternate;
        var name = alternate ? longName : shortName;
        for (var i = 0; i < elementCount; ++i)
            people[i].Name = name;
    }

    [Benchmark]
    public void FlipEveryNameObservedAsAMemberComparison() =>
        Flip();

    [Benchmark]
    public void FlipEveryNameObservedAsAMethodCall() =>
        Flip();

    [Benchmark(Baseline = true)]
    public void FlipEveryNameWithNoObservation() =>
        Flip();

    void Observe(Expression<Func<BenchmarkPerson, bool>> predicate)
    {
        SetupPeople();
        observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        observations = new IObservableExpression<BenchmarkPerson, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = observer.ObserveWithoutOptimization(predicate, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryNameWithNoObservation))]
    public void SetupBare() =>
        SetupPeople();

    [GlobalSetup(Target = nameof(FlipEveryNameObservedAsAMemberComparison))]
    public void SetupMemberComparison() =>
        Observe(memberComparison);

    [GlobalSetup(Target = nameof(FlipEveryNameObservedAsAMethodCall))]
    public void SetupMethodCall() =>
        Observe(methodCall);

    void SetupPeople()
    {
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = new BenchmarkPerson(shortName, i);
    }
}
