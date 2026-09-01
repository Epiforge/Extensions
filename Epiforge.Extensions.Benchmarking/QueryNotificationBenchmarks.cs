namespace Epiforge.Extensions.Benchmarking;

using System.Collections.Specialized;
using System.Linq.Expressions;

[MemoryDiagnoser]
public class QueryNotificationBenchmarks
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

    [GlobalCleanup(Targets = [nameof(FlipEveryMembershipWithASubscriber), nameof(FlipEveryMembershipWithNothingObserving)])]
    public void Cleanup()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(FlipEveryRankObservedWithoutAQuery))]
    public void CleanupObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            observations[i].Dispose();
    }

    [Benchmark]
    public void FlipEveryMembershipWithASubscriber()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void FlipEveryMembershipWithNothingObserving()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark]
    public void FlipEveryRankObservedWithoutAQuery()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark(Baseline = true)]
    public void FlipEveryRankWithNoQuery()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    static void Ignore(object? sender, NotifyCollectionChangedEventArgs e)
    {
    }

    [GlobalSetup(Target = nameof(FlipEveryRankWithNoQuery))]
    public void SetupBare() =>
        SetupPeople();

    [GlobalSetup(Target = nameof(FlipEveryRankObservedWithoutAQuery))]
    public void SetupExpressions()
    {
        SetupPeople();
        expressionObserver = new ExpressionObserver();
        observations = new IObservableExpression<BenchmarkPerson, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = expressionObserver.ObserveWithoutOptimization(predicate, people[i]);
    }

    [GlobalSetup(Target = nameof(FlipEveryMembershipWithASubscriber))]
    public void SetupObserved()
    {
        SetupQuery();
        where.CollectionChanged += Ignore;
    }

    void SetupPeople()
    {
        source = BenchmarkPerson.CreateCollection(elementCount);
        people = new BenchmarkPerson[elementCount];
        for (var i = 0; i < elementCount; ++i)
            people[i] = source[i];
    }

    void SetupQuery()
    {
        SetupPeople();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyList(source);
        where = sourceQuery.ObserveWhere(predicate);
    }

    [GlobalSetup(Target = nameof(FlipEveryMembershipWithNothingObserving))]
    public void SetupUnobserved() =>
        SetupQuery();
}
