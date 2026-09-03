namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
public class CrossingCostBenchmarks
{
    const int elementCount = 1000;

    static readonly Expression<Func<BenchmarkPerson, bool>> predicate = person => (person.Rank & 1) == 0;

    ExpressionObserver expressionObserver = null!;
    IObservableExpression<BenchmarkPerson, bool>[] observations = null!;
    CollectionObserver observer = null!;
    BenchmarkPerson[] people = null!;
    ObservableRangeCollection<BenchmarkPerson> source = null!;
    IObservableCollectionQuery<BenchmarkPerson> sourceQuery = null!;
    IObservableCollectionQuery<BenchmarkPerson> where = null!;

    [GlobalCleanup(Target = nameof(FlipEveryRankObservedWithASubscriber))]
    public void CleanupSubscribedObservations()
    {
        for (var i = 0; i < elementCount; ++i)
        {
            observations[i].PropertyChanged -= Ignore;
            observations[i].Dispose();
        }
    }

    [GlobalCleanup(Target = nameof(FlipEveryRankObservedWithNoSubscriber))]
    public void CleanupUnsubscribedObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            observations[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(FlipEveryMembershipInAWhereQuery))]
    public void CleanupWhereQuery()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    void Flip()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void FlipEveryMembershipInAWhereQuery() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedWithASubscriber() =>
        Flip();

    [Benchmark]
    public void FlipEveryRankObservedWithNoSubscriber() =>
        Flip();

    [Benchmark(Baseline = true)]
    public void FlipEveryRankWithNoObservation() =>
        Flip();

    static void Ignore(object? sender, PropertyChangedEventArgs e)
    {
    }

    void ObserveEveryElement()
    {
        expressionObserver = new ExpressionObserver();
        observations = new IObservableExpression<BenchmarkPerson, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = expressionObserver.ObserveWithoutOptimization(predicate, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithNoObservation))]
    public void SetupBare() =>
        SetupPeople();

    void SetupPeople()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = source[i];
    }

    [GlobalSetup(Target = nameof(FlipEveryRankObservedWithASubscriber))]
    public void SetupSubscribedObservations()
    {
        SetupPeople();
        ObserveEveryElement();
        for (var i = 0; i < elementCount; ++i)
            observations[i].PropertyChanged += Ignore;
    }

    [GlobalSetup(Target = nameof(FlipEveryRankObservedWithNoSubscriber))]
    public void SetupUnsubscribedObservations()
    {
        SetupPeople();
        ObserveEveryElement();
    }

    [GlobalSetup(Target = nameof(FlipEveryMembershipInAWhereQuery))]
    public void SetupWhereQuery()
    {
        SetupPeople();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(predicate);
    }
}
